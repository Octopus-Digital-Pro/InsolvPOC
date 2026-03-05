using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Infrastructure;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Core.Services;

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
  private readonly IApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;
  private readonly IAuditService _audit;
  private readonly DeadlineEngine _deadlineEngine;
  private readonly MailMergeService _mailMerge;
  private readonly IONRCFirmService _onrc;
  private readonly IFileStorageService _storage;
  private readonly ILogger<CaseCreationService> _logger;

  public CaseCreationService(
      IApplicationDbContext db,
      ICurrentUserService currentUser,
 IAuditService audit,
      DeadlineEngine deadlineEngine,
      MailMergeService mailMerge,
      IONRCFirmService onrc,
      IFileStorageService storage,
      ILogger<CaseCreationService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _audit = audit;
    _deadlineEngine = deadlineEngine;
    _mailMerge = mailMerge;
    _onrc = onrc;
    _storage = storage;
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
    var registrar = request.Registrar ?? upload.DetectedRegistrar;
    var caseNumber = request.CaseNumber ?? upload.DetectedCaseNumber ?? "NEW";

    var parties = ResolveParties(request.Parties, upload.DetectedPartiesJson);

    // Step 1: Create/find Company records
    var partyCompanyMap = await CreateOrFindCompaniesAsync(parties, tenantId);

    var debtorIdx = parties.FindIndex(p => p.Role == "Debtor");
    Guid? debtorCompanyId = debtorIdx >= 0 ? partyCompanyMap[debtorIdx]
 : upload.MatchedCompanyId ?? request.CompanyId;

    // Resolve DebtorCui: use party fiscal ID, then request DebtorCui, then DB lookup
    var debtorCuiFromParties = parties.FirstOrDefault(p => p.Role == "Debtor")?.FiscalId;
    if (string.IsNullOrEmpty(debtorCuiFromParties))
      debtorCuiFromParties = request.DebtorCui;  // AI-extracted CUI from ConfirmUploadCommand
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
      Registrar = registrar,
      DebtorName = debtorName,
      DebtorCui = debtorCuiFromParties,
      ProcedureType = procedureType,
      Status = "Active",
      StatusChangedAt = DateTime.UtcNow,
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
    var documentSummaries = LocalizedSummaryBuilder.BuildDocumentSummaryByLanguage(
      upload.OriginalFileName,
      upload.DetectedDocType ?? "original_notice",
      caseNumber,
      debtorName);

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
      Summary = documentSummaries["en"],
      SummaryByLanguageJson = JsonSerializer.Serialize(documentSummaries),
      // StorageKey will be set after moving the file to canonical path
      StorageKey = upload.FilePath,
      Purpose = "Uploaded",
      ClassificationConfidence = (int)(upload.Confidence * 100),
    };
    _db.InsolvencyDocuments.Add(doc);

    // Move uploaded file from TempUploads to canonical case storage path
    // (fire-and-ignore on failure — the DB record is created regardless)
    await MoveUploadedFileAsync(upload, newCase.Id, doc, tenantId);

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

    // Step 6: Auto-generate tasks per workflow
    var tasks = new List<CompanyTask>();
    if (debtorCompanyId.HasValue)
    {
      tasks = GenerateIntakeTasks(newCase, debtorCompanyId.Value, tenantId ?? Guid.Empty,
               noticeDate, baselineDeadlines, nextHearing, userId, email);
      _db.CompanyTasks.AddRange(tasks);
    }

    // Step 7: Schedule reminder emails
    var emails = GenerateReminderEmails(newCase, tenantId ?? Guid.Empty,
 noticeDate, claimsDeadline, contestationsDeadline, nextHearing, email);
    _db.ScheduledEmails.AddRange(emails);

    await _db.SaveChangesAsync();

    // Fire-and-forget: create standard folder structure in storage
    _ = InitialiseCaseFoldersAsync(newCase.Id);

    // Step 8: Auto-generate key documents (fire-and-forget)
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

    // Step 9: Audit log
    await _audit.LogAsync(new AuditEntry
    {
      Action = "Case.CreatedFromUpload",
      Description = $"A new insolvency case '{newCase.CaseNumber}' for debtor '{newCase.DebtorName}' was created " +
$"from an uploaded notice document dated {noticeDate:dd MMM yyyy}. " +
 $"The system registered {partyCompanyMap.Count} companies, linked {partiesCreated} parties, " +
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
      Status = newCase.Status,
      CompaniesCreated = partyCompanyMap.Count,
      PartiesCreated = partiesCreated,
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

  /// <summary>
  /// Moves the uploaded temp file from TempUploads into the canonical
  /// <c>cases/{caseId}/{docType}/{docId}{ext}</c> storage path and updates
  /// the document record's StorageKey in memory (caller saves to DB later).
  /// </summary>
  private async Task MoveUploadedFileAsync(
      PendingUpload upload, Guid caseId, InsolvencyDocument doc, Guid? tenantId)
  {
    if (string.IsNullOrWhiteSpace(upload.FilePath) || !File.Exists(upload.FilePath))
    {
      _logger.LogWarning("Temp upload file not found at '{Path}' — StorageKey left as temp path", upload.FilePath);
      return;
    }

    try
    {
      var docType  = upload.DetectedDocType ?? "original_notice";
      var ext      = Path.GetExtension(upload.OriginalFileName).ToLowerInvariant();
      if (ext.Length == 0) ext = ".pdf";
      var key      = CaseStorageKeys.Document(caseId, docType, doc.Id, ext);
      var mimeType = ext == ".pdf" ? "application/pdf" : "application/octet-stream";

      await _storage.EnsureFolderAsync(CaseStorageKeys.Folder(caseId, docType));

      await using var fs = File.OpenRead(upload.FilePath);
      await _storage.UploadAsync(key, fs, mimeType);

      doc.StorageKey = key;
      _logger.LogInformation("Moved upload to canonical key: {Key}", key);

      // Clean up temp file (best-effort)
      try { File.Delete(upload.FilePath); }
      catch (Exception ex) { _logger.LogDebug(ex, "Could not delete temp file {Path}", upload.FilePath); }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to move upload to canonical storage — file remains at temp path");
    }
  }

  /// <summary>Fire-and-forget: ensures all standard folders exist for the case.</summary>
  private async Task InitialiseCaseFoldersAsync(Guid caseId)
  {
    foreach (var folder in CaseStorageKeys.StandardFolders(caseId))
    {
      try { await _storage.EnsureFolderAsync(folder); }
      catch (Exception ex) { _logger.LogDebug(ex, "Could not ensure folder {Folder}", folder); }
    }
  }

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

  public List<CompanyTask> GenerateIntakeTasks(
      InsolvencyCase cas, Guid companyId, Guid tenantId,
      DateTime noticeDate, Dictionary<string, DateTime> deadlines,
      DateTime? nextHearing, Guid? userId, string email)
  {
    var tasks = new List<CompanyTask>();

    // Intake verification
    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Confirmare dată notificare și tip procedură — {cas.DebtorName}",
      "Verificați că data notificării extrase este corectă și tipul procedurii este clasificat corect.",
      "Review", noticeDate.AddDays(1), "Notice", true, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Verificare identitate debitor — {cas.DebtorName}",
      $"Confirmați CUI/CIF, nr. Registrul Comerțului și datele oficiale ale debitorului {cas.DebtorName}.",
      "Review", noticeDate.AddDays(2), "CompanyDefault", false, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Atribuire responsabil dosar — {cas.DebtorName}",
      "Asigurați că un practician responsabil este desemnat ca titular al dosarului.",
      "Compliance", noticeDate.AddDays(1), "CompanyDefault", false, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Verificare cont ONRC — {cas.DebtorName}",
      $"Verificați datele companiei {cas.DebtorName} (CUI: {cas.DebtorCui}) în registrul ONRC și actualizați fișa.",
      "Review", noticeDate.AddDays(2), "CompanyDefault", false, userId));

    // Notifications
    if (deadlines.TryGetValue("initialNoticeSendBy", out var noticeSendBy))
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id,
        $"Generare notificări inițiale (Templates-Ro) — {cas.DebtorName}",
        "Generați notificarea către creditori și anunțul de publicare BPI din șabloane.",
        "Document", noticeSendBy, "CompanyDefault", true, userId));
    }

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Notificare deschidere procedură — {cas.DebtorName}",
      $"Publicare în BPI și notificare ONRC pentru dosarul {cas.CaseNumber}.",
      "Email", noticeDate.AddDays(3), "CompanyDefault", true, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Notificare creditori cunoscuți — {cas.DebtorName}",
      "Trimiteți notificări către toți creditorii identificați din documentele primite.",
      "Email", noticeDate.AddDays(5), "CompanyDefault", false, userId));

    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Notificare ANAF și autorități fiscale — {cas.DebtorName}",
      $"Transmiteți notificarea de deschidere către ANAF și autoritățile locale pentru dosarul {cas.CaseNumber}.",
      "Email", noticeDate.AddDays(4), "CompanyDefault", true, userId));

    // Causes report
    tasks.Add(CreateTask(tenantId, companyId, cas.Id,
      $"Raport Art. 97 Legea 85/2014 — {cas.DebtorName}",
      $"Întocmire raport privind cauzele și împrejurările care au dus la apariția stării de insolvență — dosar {cas.CaseNumber}.",
      "Report", noticeDate.AddDays(40), "CompanyDefault", false, userId));

    // Claims table
    if (deadlines.TryGetValue("claimDeadline", out var claimDl))
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id,
        $"Verificare și întocmire tabel preliminar creanțe — {cas.DebtorName}",
        $"Termen creanțe: {claimDl:dd.MM.yyyy}. Verificați declarațiile de creanță primite și întocmiți tabelul preliminar.",
        "Document", claimDl.AddDays(5), "CompanyDefault", true, userId));
    }

    // Court hearing
    if (nextHearing.HasValue)
    {
      tasks.Add(CreateTask(tenantId, companyId, cas.Id,
        $"Pregătire termen de judecată — {cas.DebtorName}",
        $"Termen: {nextHearing.Value:dd.MM.yyyy} la {cas.CourtName}. Pregătiți rapoartele și documentele necesare.",
        "Filing", nextHearing.Value.AddDays(-2), "Notice", false, userId));
    }

    return tasks;
  }

  private static CompanyTask CreateTask(
    Guid tenantId, Guid companyId, Guid caseId,
    string title, string description, string category,
    DateTime deadline, string deadlineSource, bool isCritical, Guid? assigneeId)
  {
    var summaries = LocalizedSummaryBuilder.BuildTaskSummaryByLanguage(
      title,
      description,
      category,
      deadline,
      TaskStatus.Open);

    return new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      CompanyId = companyId,
      CaseId = caseId,
      Title = title,
      Description = description,
      Category = category,
      Deadline = deadline,
      DeadlineSource = deadlineSource,
      IsCriticalDeadline = isCritical,
      Status = TaskStatus.Open,
      Summary = summaries["en"],
      SummaryByLanguageJson = JsonSerializer.Serialize(summaries),
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
      $"[Insolvex] Termen creanțe în 7 zile — {cas.DebtorName} ({cas.CaseNumber})",
      $"Termenul limită pentru depunerea creanțelor în dosarul {cas.CaseNumber} ({cas.DebtorName}) este {claimsDeadline?.ToString("dd.MM.yyyy")}.\n\nMai aveți 7 zile.");

    AddReminder(claimsDeadline, 1,
      $"[Insolvex] URGENT: Termen creanțe MÎINE — {cas.DebtorName}",
      $"ATENȚIE: Termenul pentru creanțe în dosarul {cas.CaseNumber} expiră MÎINE {claimsDeadline?.ToString("dd.MM.yyyy")}. ");

    // Hearing reminder
    AddReminder(nextHearing, 2,
      $"[Insolvex] Termen de judecată în 2 zile — {cas.DebtorName}",
      $"Dosarul {cas.CaseNumber}: termen la {cas.CourtName} pe {nextHearing?.ToString("dd.MM.yyyy")}.\n\nPregătiți documentele.");

    // Contestations reminder
    AddReminder(contestationsDeadline, 3,
      $"[Insolvex] Termen contestații în 3 zile — {cas.DebtorName}",
      $"Termenul pentru contestatii tabel creante: {contestationsDeadline?.ToString("dd.MM.yyyy")}. ");

            return emails;
  }

  /// <summary>
  /// Finds an existing company by CUI or name (for the tenant), or creates a new one.
  /// Returns the company Id, and optionally links it to the case as Debtor.
  /// </summary>
  public async Task<Guid?> ResolveOrCreateCompanyAsync(
    string debtorName, string? debtorCui, Guid tenantId,
    string? createdBy, Guid caseId, CancellationToken ct)
  {
    Company? company = null;

    if (!string.IsNullOrWhiteSpace(debtorCui))
    {
      company = await _db.Companies
        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CuiRo == debtorCui.Trim(), ct);
    }

    if (company is null && !string.IsNullOrWhiteSpace(debtorName))
    {
      var nameLower = debtorName.Trim().ToLower();
      company = await _db.Companies
        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name.ToLower() == nameLower, ct);
    }

    if (company is null)
    {
      company = new Company
      {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = debtorName.Trim(),
        CuiRo = debtorCui?.Trim(),
        CreatedOn = DateTime.UtcNow,
        CreatedBy = createdBy ?? "System",
      };
      _db.Companies.Add(company);
      await _db.SaveChangesAsync(ct);
    }

    // Ensure a Debtor CaseParty link exists
    var partyExists = await _db.CaseParties
      .AnyAsync(p => p.CaseId == caseId && p.CompanyId == company.Id, ct);
    if (!partyExists)
    {
      _db.CaseParties.Add(new CaseParty
      {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CaseId = caseId,
        CompanyId = company.Id,
        Role = CasePartyRole.Debtor,
        CreatedOn = DateTime.UtcNow,
        CreatedBy = createdBy ?? "System",
      });
      await _db.SaveChangesAsync(ct);
    }

    return company.Id;
  }
}

// ?? Request / Result models ?????????????????????????????

public class CaseCreationRequest
{
  public string? CaseNumber { get; set; }
  public string? CourtName { get; set; }
  public string? CourtSection { get; set; }
  public string? DebtorName { get; set; }
  /// <summary>Romanian CUI from AI extraction — used for company lookup/creation if not found via parties.</summary>
  public string? DebtorCui { get; set; }
  public string? JudgeSyndic { get; set; }
  public string? Registrar { get; set; }
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
  public string Status { get; set; } = "Active";
  public int CompaniesCreated { get; set; }
  public int PartiesCreated { get; set; }
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
