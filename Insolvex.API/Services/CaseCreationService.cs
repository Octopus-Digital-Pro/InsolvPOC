using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.API.Services;

/// <summary>
/// Orchestrates the full case creation flow per InsolvencyAppRules Stage 0:
///   1. Accept confirmed upload data (NoticeDate, parties, dates)
///   2. Create/find Company records for each party
///3. Create InsolvencyCase with NoticeDate as CaseCreationDate
///   4. Attach uploaded document with extraction metadata
///   5. Create CaseParties
///   6. Initialize CasePhases for the procedure type
/// 7. Compute baseline deadlines from NoticeDate via DeadlineEngine
///   8. Auto-generate stage tasks
///   9. Schedule reminder emails
///  10. Generate key documents via MailMergeService (fire-and-forget)
///  11. Return the full creation result
/// </summary>
public class CaseCreationService
{
  private readonly ApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;
  private readonly IAuditService _audit;
  private readonly DeadlineEngine _deadlineEngine;
  private readonly MailMergeService _mailMerge;
  private readonly IONRCFirmService _onrc;
  private readonly ILogger<CaseCreationService> _logger;

  public CaseCreationService(
      ApplicationDbContext db,
      ICurrentUserService currentUser,
 IAuditService audit,
      DeadlineEngine deadlineEngine,
      MailMergeService mailMerge,
      IONRCFirmService onrc,
      ILogger<CaseCreationService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _audit = audit;
    _deadlineEngine = deadlineEngine;
    _mailMerge = mailMerge;
    _onrc = onrc;
    _logger = logger;
  }

  /// <summary>
  /// Create a full case from a confirmed pending upload.
  /// </summary>
  public async Task<CaseCreationResult> CreateFromUploadAsync(PendingUpload upload, CaseCreationRequest request)
  {
    var tenantId = _currentUser.TenantId ?? upload.TenantId;
    var userId = _currentUser.UserId;
    var email = _currentUser.Email ?? "System";

    // Resolve inputs (request overrides upload detection)
    var procedureType = ResolveProcedureType(request.ProcedureType, upload.DetectedProcedureType);
    var noticeDate = request.NoticeDate ?? upload.DetectedOpeningDate ?? DateTime.UtcNow;
    var openingDate = request.OpeningDate ?? upload.DetectedOpeningDate ?? noticeDate;
    var nextHearing = request.NextHearingDate ?? upload.DetectedNextHearingDate;
    var claimsDeadline = request.ClaimsDeadline ?? upload.DetectedClaimsDeadline;
    var contestationsDeadline = request.ContestationsDeadline ?? upload.DetectedContestationsDeadline;
    var debtorName = request.DebtorName ?? upload.DetectedDebtorName ?? "Unknown Debtor";
    var courtName = request.CourtName ?? upload.DetectedCourtName;
    var courtSection = request.CourtSection ?? upload.DetectedCourtSection;
    var judgeSyndic = request.JudgeSyndic ?? upload.DetectedJudgeSyndic;
    var caseNumber = request.CaseNumber ?? upload.DetectedCaseNumber ?? "NEW";

    var parties = ResolveParties(request.Parties, upload.DetectedPartiesJson);

    // Step 1: Create/find Company records
    var partyCompanyMap = await CreateOrFindCompaniesAsync(parties, tenantId);

    var debtorIdx = parties.FindIndex(p => p.Role == "Debtor");
    Guid? debtorCompanyId = debtorIdx >= 0 ? partyCompanyMap[debtorIdx]
 : upload.MatchedCompanyId ?? request.CompanyId;

    // Resolve DebtorCui: use party fiscal ID, or fall back to ONRC-enriched company CUI
    var debtorCuiFromParties = parties.FirstOrDefault(p => p.Role == "Debtor")?.FiscalId;
    if (string.IsNullOrEmpty(debtorCuiFromParties) && debtorCompanyId.HasValue)
    {
      var debtorCo = await _db.Companies.AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == debtorCompanyId.Value);
      debtorCuiFromParties = debtorCo?.CuiRo;
    }

    // Step 2: Compute baseline deadlines from NoticeDate
    var baselineDeadlines = await _deadlineEngine.ComputeBaselineDeadlinesAsync(noticeDate, tenantId);
    claimsDeadline ??= baselineDeadlines.GetValueOrDefault("claimDeadline");
    contestationsDeadline ??= baselineDeadlines.GetValueOrDefault("objectionDeadline");

    // Step 3: Create the InsolvencyCase
    var newCase = new InsolvencyCase
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId ?? Guid.Empty,
      CaseNumber = caseNumber,
      CourtName = courtName,
      CourtSection = courtSection,
      JudgeSyndic = judgeSyndic,
      DebtorName = debtorName,
      DebtorCui = debtorCuiFromParties,
      ProcedureType = procedureType,
      Stage = CaseStage.Intake,
      StageEnteredAt = DateTime.UtcNow,
      LawReference = "Legea 85/2014",
      PractitionerName = parties.FirstOrDefault(p => p.Role == "InsolvencyPractitioner")?.Name,
      PractitionerRole = procedureType is ProcedureType.FalimentSimplificat or ProcedureType.Faliment
            ? "lichidator_judiciar" : "administrator_judiciar",
      PractitionerFiscalId = parties.FirstOrDefault(p => p.Role == "InsolvencyPractitioner")?.FiscalId,
      CompanyId = debtorCompanyId,
      AssignedToUserId = userId,
      NoticeDate = noticeDate,
      OpeningDate = openingDate,
      NextHearingDate = nextHearing,
      ClaimsDeadline = claimsDeadline,
      ContestationsDeadline = contestationsDeadline,
      KeyDeadlinesJson = JsonSerializer.Serialize(baselineDeadlines),
    };
    _db.InsolvencyCases.Add(newCase);

    // Step 4: Attach the uploaded document
    var doc = new InsolvencyDocument
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId ?? Guid.Empty,
      CaseId = newCase.Id,
      SourceFileName = upload.OriginalFileName,
      DocType = upload.DetectedDocType ?? "original_notice",
      UploadedBy = email,
      UploadedAt = upload.UploadedAt,
      RawExtraction = upload.ExtractedText,
      Summary = $"Original notice document for case {caseNumber}. Debtor: {debtorName}.",
      StorageKey = upload.FilePath,
      Purpose = "Uploaded",
      ClassificationConfidence = (int)(upload.Confidence * 100),
    };
    _db.InsolvencyDocuments.Add(doc);

    // Step 5: Create CaseParties
    var partiesCreated = 0;
    for (var i = 0; i < parties.Count; i++)
    {
      var p = parties[i];
      if (!Enum.TryParse<CasePartyRole>(p.Role, true, out var role)) continue;

      _db.CaseParties.Add(new CaseParty
      {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? Guid.Empty,
        CaseId = newCase.Id,
        CompanyId = partyCompanyMap[i],
        Role = role,
        ClaimAmountRon = p.ClaimAmount,
        JoinedDate = openingDate,
      });
      partiesCreated++;
    }

    // Step 6: Initialize CasePhases
    var phases = GetPhasesForProcedure(procedureType);
    InitializeCasePhases(newCase.Id, tenantId ?? Guid.Empty, phases, openingDate, claimsDeadline);

    // Step 7: Auto-generate tasks per Stage 0 workflow
    var tasks = new List<CompanyTask>();
    if (debtorCompanyId.HasValue)
    {
      tasks = GenerateIntakeTasks(newCase, debtorCompanyId.Value, tenantId ?? Guid.Empty,
               noticeDate, baselineDeadlines, nextHearing, userId, email);
      _db.CompanyTasks.AddRange(tasks);
    }

    // Step 8: Schedule reminder emails
    var emails = GenerateReminderEmails(newCase, tenantId ?? Guid.Empty,
 noticeDate, claimsDeadline, contestationsDeadline, nextHearing, email);
    _db.ScheduledEmails.AddRange(emails);

    await _db.SaveChangesAsync();

    // Step 9: Auto-generate key documents (fire-and-forget)
    var generatedDocs = new List<GeneratedDocument>();
    try
    {
      var debtorCompany = debtorCompanyId.HasValue
? await _db.Companies.FirstOrDefaultAsync(c => c.Id == debtorCompanyId.Value)
          : null;
      var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();

      generatedDocs = await _mailMerge.GenerateKeyDocumentsForCaseAsync(
newCase, debtorCompany, firm, upload.DetectedDocType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to auto-generate key documents for case {CaseId}", newCase.Id);
    }

    // Step 10: Audit log
    await _audit.LogAsync(new AuditEntry
    {
      Action = "Case.CreatedFromUpload",
      Description = $"A new insolvency case '{newCase.CaseNumber}' for debtor '{newCase.DebtorName}' was created " +
$"from an uploaded notice document dated {noticeDate:dd MMM yyyy}. " +
 $"The system registered {partyCompanyMap.Count} companies, linked {partiesCreated} parties, " +
$"initialized {phases.Count} workflow phases for {procedureType} procedure, " +
      $"auto-generated {tasks.Count} tasks, and scheduled {emails.Count} reminder emails.",
      EntityType = "InsolvencyCase",
      EntityId = newCase.Id,
      EntityName = newCase.CaseNumber,
      CaseNumber = newCase.CaseNumber,
      NewValues = new
      {
        newCase.CaseNumber,
        newCase.DebtorName,
        procedureType = procedureType.ToString(),
        noticeDate,
        companiesCreated = partyCompanyMap.Count,
        partiesCreated,
        phasesCreated = phases.Count,
        tasksCreated = tasks.Count,
        emailsScheduled = emails.Count,
        documentsGenerated = generatedDocs.Count,
      },
      Severity = "Critical",
      Category = "CaseCreation",
    });

    return new CaseCreationResult
    {
      CaseId = newCase.Id,
      DocumentId = doc.Id,
      CaseNumber = newCase.CaseNumber,
      NoticeDate = noticeDate,
      Stage = newCase.Stage,
      CompaniesCreated = partyCompanyMap.Count,
      PartiesCreated = partiesCreated,
      PhasesCreated = phases.Count,
      TasksCreated = tasks.Count,
      EmailsScheduled = emails.Count,
      DocumentsGenerated = generatedDocs.Count,
      BaselineDeadlines = baselineDeadlines,
      GeneratedDocuments = generatedDocs.Select(d => new GeneratedDocInfo
      {
        TemplateType = d.TemplateType.ToString(),
        FileName = d.FileName,
        StorageKey = d.StorageKey,
      }).ToList(),
    };
  }

  // ?? Private helpers ?????????????????????????????????????

  private static ProcedureType ResolveProcedureType(string? requestType, ProcedureType? detected)
  {
    if (requestType != null && Enum.TryParse<ProcedureType>(requestType, true, out var pt))
      return pt;
    return detected ?? ProcedureType.Other;
  }

  private static List<CaseCreationParty> ResolveParties(List<CaseCreationParty>? requestParties, string? detectedJson)
  {
    if (requestParties?.Count > 0) return requestParties;
    if (!string.IsNullOrEmpty(detectedJson))
    {
      try
      {
        return JsonSerializer.Deserialize<List<CaseCreationParty>>(detectedJson) ?? new();
      }
      catch { /* fall through */ }
    }
    return new();
  }

  private async Task<Dictionary<int, Guid>> CreateOrFindCompaniesAsync(
      List<CaseCreationParty> parties, Guid? tenantId)
  {
    var map = new Dictionary<int, Guid>();
    var region = await GetTenantRegionAsync(tenantId);

    for (var i = 0; i < parties.Count; i++)
    {
      var p = parties[i];
      var companyType = p.Role switch
      {
        "Debtor" => CompanyType.Debtor,
        "InsolvencyPractitioner" => CompanyType.InsolvencyPractitioner,
        "Court" => CompanyType.Court,
        "BudgetaryCreditor" or "SecuredCreditor" or "UnsecuredCreditor" or "EmployeeCreditor" => CompanyType.Creditor,
        _ => CompanyType.Other,
      };

      // Try to find existing by name or fiscal ID
      var existing = await _db.Companies
        .FirstOrDefaultAsync(c => c.Name == p.Name || (p.FiscalId != null && c.CuiRo == p.FiscalId));

      if (existing != null)
      {
        map[i] = existing.Id;
      }
      else
      {
        var company = new Company
        {
          Id = Guid.NewGuid(),
          TenantId = tenantId ?? Guid.Empty,
          Name = p.Name ?? "Unknown",
          CompanyType = companyType,
          CuiRo = p.FiscalId,
        };

        // Enrich Debtor (and other parties with a name/CUI) from the ONRC database
        if (!string.IsNullOrWhiteSpace(p.Name) || !string.IsNullOrWhiteSpace(p.FiscalId))
        {
          try
          {
            var onrcQuery = p.FiscalId ?? p.Name ?? "";
            var onrcHits = await _onrc.SearchAsync(onrcQuery, region, 1, CancellationToken.None);
            if (onrcHits.Count > 0)
            {
              var hit = onrcHits[0];
              company.CuiRo ??= hit.CUI;
              company.Address ??= hit.Address;
              company.Locality ??= hit.Locality;
              company.County ??= hit.County;
              _logger.LogInformation(
                "ONRC: enriched company '{Name}' (CUI: {CUI}) from national registry",
                company.Name, hit.CUI);
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "ONRC lookup failed for party '{Name}' — continuing without enrichment", p.Name);
          }
        }

        _db.Companies.Add(company);
        map[i] = company.Id;
      }
    }
    return map;
  }

  private async Task<Insolvex.Domain.Enums.SystemRegion> GetTenantRegionAsync(Guid? tenantId)
  {
    if (!tenantId.HasValue) return Insolvex.Domain.Enums.SystemRegion.Romania;
    var tenant = await _db.Tenants.AsNoTracking()
      .FirstOrDefaultAsync(t => t.Id == tenantId.Value);
    return tenant?.Region ?? Insolvex.Domain.Enums.SystemRegion.Romania;
  }

  private void InitializeCasePhases(Guid caseId, Guid tenantId,
  List<PhaseType> phases, DateTime openingDate, DateTime? claimsDeadline)
  {
    var sortOrder = 1;
    foreach (var phaseType in phases)
    {
      var phase = new CasePhase
      {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CaseId = caseId,
        PhaseType = phaseType,
        Status = sortOrder == 1 ? PhaseStatus.Completed
        : sortOrder == 2 ? PhaseStatus.InProgress
 : PhaseStatus.NotStarted,
        SortOrder = sortOrder,
      };

      if (sortOrder == 1)
      {
        phase.StartedOn = openingDate;
        phase.CompletedOn = openingDate;
      }
      else if (sortOrder == 2)
      {
        phase.StartedOn = openingDate;
        phase.DueDate = claimsDeadline;
      }

      _db.CasePhases.Add(phase);
      sortOrder++;
    }
  }

  private List<CompanyTask> GenerateIntakeTasks(
      InsolvencyCase cas, Guid companyId, Guid tenantId,
  DateTime noticeDate, Dictionary<string, DateTime> deadlines,
      DateTime? nextHearing, Guid? userId, string email)
  {
    var tasks = new List<CompanyTask>();
    var settings = new DeadlineSettings(); // use defaults for buffer calculations

    // Stage 0 required tasks per InsolvencyAppRules
    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.Intake,
      $"Confirm NoticeDate and case type � {cas.DebtorName}",
"Verify the extracted NoticeDate is correct and case type is properly classified.",
"Review", noticeDate.AddDays(1), "Notice", true, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.Intake,
$"Verify debtor identity � {cas.DebtorName}",
        "Confirm debtor CUI/VAT, trade register details from official sources.",
     "Review", noticeDate.AddDays(2), "CompanyDefault", false, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.Intake,
        $"Assign case owner � {cas.DebtorName}",
"Ensure a responsible practitioner is assigned as case lead.",
"Compliance", noticeDate.AddDays(1), "CompanyDefault", false, userId));

    // Stage 1 tasks (auto-created ahead)
    if (deadlines.TryGetValue("initialNoticeSendBy", out var noticeSendBy))
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.EligibilitySetup,
       $"Generate initial notices (Templates-Ro) � {cas.DebtorName}",
          "Generate creditor notification and BPI publication notice from templates.",
                   "Document", noticeSendBy, "CompanyDefault", true, userId));
    }

    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.FormalNotifications,
    $"Notificare deschidere procedur? � {cas.DebtorName}",
        $"Publicare �n BPI ?i notificare ONRC pentru dosarul {cas.CaseNumber}.",
          "Email", noticeDate.AddDays(3), "CompanyDefault", true, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.FormalNotifications,
                $"Notificare creditori cunoscu?i � {cas.DebtorName}",
                "Trimite notific?ri c?tre to?i creditorii identifica?i din documentele primite.",
                "Email", noticeDate.AddDays(5), "CompanyDefault", false, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.AssetAssessment,
        $"Raport Art. 97 � {cas.DebtorName}",
        $"�ntocmire raport privind cauzele ?i �mprejur?rile care au dus la apari?ia st?rii de insolven?? � dosar {cas.CaseNumber}.",
"Report", noticeDate.AddDays(40), "CompanyDefault", false, userId));

    if (deadlines.TryGetValue("claimDeadline", out var claimDl))
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.CreditorClaims,
         $"Verificare ?i �ntocmire tabel preliminar crean?e � {cas.DebtorName}",
    $"Termen crean?e: {claimDl:dd.MM.yyyy}. Verificare declara?ii de crean?? primite.",
                "Document", claimDl.AddDays(5), "CompanyDefault", true, userId));
    }

    if (nextHearing.HasValue)
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id, CaseStage.EligibilitySetup,
$"Preg?tire termen de judecat? � {cas.DebtorName}",
$"Termen: {nextHearing.Value:dd.MM.yyyy} la {cas.CourtName}. Preg?tire rapoarte ?i documente.",
             "Filing", nextHearing.Value.AddDays(-2), "Notice", false, userId));
    }

    return tasks;
  }

  private static CompanyTask CreateTask(
   Guid tenantId, Guid companyId, Guid caseId, CaseStage stage,
      string title, string description, string category,
      DateTime deadline, string deadlineSource, bool isCritical, Guid? assigneeId)
  {
    return new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      CompanyId = companyId,
      CaseId = caseId,
      Stage = stage,
      Title = title,
      Description = description,
      Category = category,
      Deadline = deadline,
      DeadlineSource = deadlineSource,
      IsCriticalDeadline = isCritical,
      Status = TaskStatus.Open,
      AssignedToUserId = assigneeId,
      CreatedByUserId = assigneeId,
    };
  }

  private List<ScheduledEmail> GenerateReminderEmails(
      InsolvencyCase cas, Guid tenantId,
   DateTime noticeDate, DateTime? claimsDeadline, DateTime? contestationsDeadline,
      DateTime? nextHearing, string recipient)
  {
    var emails = new List<ScheduledEmail>();
    var now = DateTime.UtcNow;

    void AddReminder(DateTime? deadline, int daysBefore, string subject, string body)
    {
      if (!deadline.HasValue) return;
      var sendAt = deadline.Value.AddDays(-daysBefore);
      if (sendAt <= now) return;
      emails.Add(new ScheduledEmail
      {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CaseId = cas.Id,
        To = recipient,
        Subject = subject,
        Body = body,
        ScheduledFor = sendAt,
        Status = "Scheduled",
      });
    }

    // Claims deadline reminders
    AddReminder(claimsDeadline, 7,
$"[Insolvex] Termen crean?e �n 7 zile � {cas.DebtorName} ({cas.CaseNumber})",
          $"Termenul limit? pentru depunerea crean?elor �n dosarul {cas.CaseNumber} ({cas.DebtorName}) este {claimsDeadline?.ToString("dd.MM.yyyy")}.\n\nMai ave?i 7 zile.");

    AddReminder(claimsDeadline, 1,
        $"[Insolvex] URGENT: Termen crean?e M�INE � {cas.DebtorName}",
 $"ATEN?IE: Termenul pentru crean?e �n dosarul {cas.CaseNumber} expir? M�INE {claimsDeadline?.ToString("dd.MM.yyyy")}.");

    // Hearing reminder
    AddReminder(nextHearing, 2,
     $"[Insolvex] Termen de judecat? �n 2 zile � {cas.DebtorName}",
  $"Dosarul {cas.CaseNumber}: termen la {cas.CourtName} pe {nextHearing?.ToString("dd.MM.yyyy")}.\n\nPreg?ti?i documentele.");

    // Contestations reminder
    AddReminder(contestationsDeadline, 3,
        $"[Insolvex] Termen contesta?ii �n 3 zile � {cas.DebtorName}",
   $"Termenul pentru contesta?ii tabel crean?e: {contestationsDeadline?.ToString("dd.MM.yyyy")}.");

    return emails;
  }

  internal static List<PhaseType> GetPhasesForProcedure(ProcedureType procedure)
  {
    return procedure switch
    {
      ProcedureType.FalimentSimplificat or ProcedureType.Faliment => new()
            {
    PhaseType.OpeningRequest, PhaseType.CreditorNotification, PhaseType.ClaimsFiling,
    PhaseType.PreliminaryClaimsTable, PhaseType.ClaimsContestations,
 PhaseType.DefinitiveClaimsTable, PhaseType.AssetLiquidation,
             PhaseType.CreditorDistribution, PhaseType.FinalReport, PhaseType.ProcedureClosure,
            },
      ProcedureType.Reorganizare => new()
       {
             PhaseType.OpeningRequest, PhaseType.ObservationPeriod, PhaseType.CreditorNotification,
            PhaseType.ClaimsFiling, PhaseType.PreliminaryClaimsTable, PhaseType.ClaimsContestations,
     PhaseType.DefinitiveClaimsTable, PhaseType.CausesReport,
       PhaseType.ReorganizationPlanProposal, PhaseType.ReorganizationPlanVoting,
   PhaseType.ReorganizationPlanConfirmation, PhaseType.ReorganizationExecution,
      PhaseType.ProcedureClosure,
      },
      ProcedureType.ConcordatPreventiv => new()
  {
    PhaseType.OpeningRequest, PhaseType.ObservationPeriod, PhaseType.CreditorNotification,
             PhaseType.ReorganizationPlanProposal, PhaseType.ReorganizationPlanVoting,
        PhaseType.ReorganizationPlanConfirmation, PhaseType.ProcedureClosure,
},
      _ => new()
            {
              PhaseType.OpeningRequest, PhaseType.ObservationPeriod, PhaseType.CreditorNotification,
                PhaseType.ClaimsFiling, PhaseType.PreliminaryClaimsTable, PhaseType.ClaimsContestations,
      PhaseType.DefinitiveClaimsTable, PhaseType.FinalReport, PhaseType.ProcedureClosure,
},
    };
  }
}

// ?? Request / Result models ?????????????????????????????

public class CaseCreationRequest
{
  public string? CaseNumber { get; set; }
  public string? CourtName { get; set; }
  public string? CourtSection { get; set; }
  public string? DebtorName { get; set; }
  public string? JudgeSyndic { get; set; }
  public string? ProcedureType { get; set; }
  public DateTime? NoticeDate { get; set; }
  public DateTime? OpeningDate { get; set; }
  public DateTime? NextHearingDate { get; set; }
  public DateTime? ClaimsDeadline { get; set; }
  public DateTime? ContestationsDeadline { get; set; }
  public Guid? CompanyId { get; set; }
  public List<CaseCreationParty>? Parties { get; set; }
}

public class CaseCreationParty
{
  public string Role { get; set; } = string.Empty;
  public string? Name { get; set; }
  public string? FiscalId { get; set; }
  public decimal? ClaimAmount { get; set; }
}

public class CaseCreationResult
{
  public Guid CaseId { get; set; }
  public Guid DocumentId { get; set; }
  public string CaseNumber { get; set; } = string.Empty;
  public DateTime NoticeDate { get; set; }
  public CaseStage Stage { get; set; }
  public int CompaniesCreated { get; set; }
  public int PartiesCreated { get; set; }
  public int PhasesCreated { get; set; }
  public int TasksCreated { get; set; }
  public int EmailsScheduled { get; set; }
  public int DocumentsGenerated { get; set; }
  public Dictionary<string, DateTime> BaselineDeadlines { get; set; } = new();
  public List<GeneratedDocInfo> GeneratedDocuments { get; set; } = new();
}

public class GeneratedDocInfo
{
  public string TemplateType { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public string StorageKey { get; set; } = string.Empty;
}
