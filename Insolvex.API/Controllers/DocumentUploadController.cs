using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

// Alias to resolve ambiguity with Core.Abstractions.ExtractedParty
using ExtractedParty = Insolvex.API.Services.ClassificationExtractedParty;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentUploadController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly DocumentClassificationService _classifier;
    private readonly MailMergeService _mailMerge;
    private readonly IWebHostEnvironment _env;

    public DocumentUploadController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        DocumentClassificationService classifier,
    MailMergeService mailMerge,
        IWebHostEnvironment env)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _classifier = classifier;
        _mailMerge = mailMerge;
        _env = env;
    }

    /// <summary>
    /// Upload a document for AI classification.
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permission.DocumentUpload)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
      if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        var allowed = new[] { ".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".tiff" };
   var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
   return BadRequest(new { message = $"File type '{ext}' not supported." });

    var uploadsDir = Path.Combine(_env.ContentRootPath, "TempUploads");
        Directory.CreateDirectory(uploadsDir);

      var storedName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, storedName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
          await file.CopyToAsync(stream);

      var c = await _classifier.ClassifyAsync(filePath, file.FileName);

        var upload = new PendingUpload
        {
 Id = Guid.NewGuid(),
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
FilePath = filePath,
     FileSize = file.Length,
     ContentType = file.ContentType,
        UploadedAt = DateTime.UtcNow,
            UploadedByUserId = _currentUser.UserId,
     UploadedByEmail = _currentUser.Email,
          TenantId = _currentUser.TenantId,
  RecommendedAction = c.RecommendedAction,
       DetectedDocType = c.DocType,
   DetectedCaseNumber = c.CaseNumber,
   DetectedDebtorName = c.DebtorName,
            DetectedCourtName = c.CourtName,
            MatchedCaseId = c.MatchedCaseId,
            MatchedCompanyId = c.MatchedCompanyId,
  ExtractedText = c.ExtractedText,
        Confidence = c.Confidence,
 // Structured
   DetectedProcedureType = c.DetectedProcedureType,
     DetectedCourtSection = c.CourtSection,
            DetectedJudgeSyndic = c.JudgeSyndic,
         DetectedOpeningDate = c.OpeningDate,
       DetectedNextHearingDate = c.NextHearingDate,
DetectedClaimsDeadline = c.ClaimsDeadline,
     DetectedContestationsDeadline = c.ContestationsDeadline,
            DetectedPartiesJson = c.Parties.Count > 0
  ? JsonSerializer.Serialize(c.Parties) : null,
        };

    _db.Set<PendingUpload>().Add(upload);
   await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("Document.Uploaded", "PendingUpload", upload.Id,
            newValues: new { upload.OriginalFileName, upload.DetectedDocType, upload.DetectedCaseNumber, upload.Confidence });

        return Ok(new UploadResponse
        {
            Id = upload.Id,
            FileName = file.FileName,
         FileSize = file.Length,
RecommendedAction = c.RecommendedAction,
       DocType = c.DocType,
            CaseNumber = c.CaseNumber,
    DebtorName = c.DebtorName,
 CourtName = c.CourtName,
  CourtSection = c.CourtSection,
     JudgeSyndic = c.JudgeSyndic,
       MatchedCaseId = c.MatchedCaseId,
       MatchedCompanyId = c.MatchedCompanyId,
          Confidence = c.Confidence,
  ProcedureType = c.DetectedProcedureType?.ToString(),
            OpeningDate = c.OpeningDate,
            NextHearingDate = c.NextHearingDate,
    ClaimsDeadline = c.ClaimsDeadline,
            ContestationsDeadline = c.ContestationsDeadline,
    Parties = c.Parties,
         ExtractedText = c.ExtractedText,
        });
    }

    [HttpGet("upload/{id:guid}")]
    public async Task<IActionResult> GetUpload(Guid id)
    {
  var u = await _db.Set<PendingUpload>().FirstOrDefaultAsync(x => x.Id == id);
   if (u == null) return NotFound();

        var parties = !string.IsNullOrEmpty(u.DetectedPartiesJson)
     ? JsonSerializer.Deserialize<List<ExtractedParty>>(u.DetectedPartiesJson) ?? new()
            : new List<ExtractedParty>();

  return Ok(new UploadResponse
    {
  Id = u.Id,
            FileName = u.OriginalFileName,
       FileSize = u.FileSize,
RecommendedAction = u.RecommendedAction,
          DocType = u.DetectedDocType,
     CaseNumber = u.DetectedCaseNumber,
   DebtorName = u.DetectedDebtorName,
            CourtName = u.DetectedCourtName,
            CourtSection = u.DetectedCourtSection,
  JudgeSyndic = u.DetectedJudgeSyndic,
            MatchedCaseId = u.MatchedCaseId,
        MatchedCompanyId = u.MatchedCompanyId,
            Confidence = u.Confidence,
            ProcedureType = u.DetectedProcedureType?.ToString(),
            OpeningDate = u.DetectedOpeningDate,
            NextHearingDate = u.DetectedNextHearingDate,
            ClaimsDeadline = u.DetectedClaimsDeadline,
ContestationsDeadline = u.DetectedContestationsDeadline,
     Parties = parties,
      ExtractedText = u.ExtractedText,
        });
    }

    /// <summary>
    /// Confirm the upload: create the full case with parties, phases, tasks, and reminder emails.
    /// </summary>
 [HttpPost("upload/{id:guid}/confirm")]
    [RequirePermission(Permission.CaseCreate)]
    public async Task<IActionResult> ConfirmUpload(Guid id, [FromBody] ConfirmUploadRequest request)
    {
        var upload = await _db.Set<PendingUpload>().FirstOrDefaultAsync(x => x.Id == id);
        if (upload == null) return NotFound();

        var tenantId = _currentUser.TenantId ?? upload.TenantId;

        if (request.Action == "filing" && request.CaseId.HasValue)
        {
   return await FileToExistingCase(upload, request.CaseId.Value);
        }

    // ?? NEW CASE CREATION FLOW ??????????????????????????

        var procedureType = request.ProcedureType != null
        && Enum.TryParse<ProcedureType>(request.ProcedureType, true, out var pt)
    ? pt
            : upload.DetectedProcedureType ?? ProcedureType.Other;

  var openingDate = request.OpeningDate ?? upload.DetectedOpeningDate ?? DateTime.UtcNow;
        var nextHearing = request.NextHearingDate ?? upload.DetectedNextHearingDate;
        var claimsDeadline = request.ClaimsDeadline ?? upload.DetectedClaimsDeadline;
        var contestationsDeadline = request.ContestationsDeadline ?? upload.DetectedContestationsDeadline;
        var debtorName = request.DebtorName ?? upload.DetectedDebtorName ?? "Unknown Debtor";
        var courtName = request.CourtName ?? upload.DetectedCourtName;
        var courtSection = request.CourtSection ?? upload.DetectedCourtSection;
  var judgeSyndic = request.JudgeSyndic ?? upload.DetectedJudgeSyndic;
        var caseNumber = request.CaseNumber ?? upload.DetectedCaseNumber ?? "NEW";

        // Parse parties from request or fallback to upload
        var parties = request.Parties?.Count > 0
   ? request.Parties
            : (!string.IsNullOrEmpty(upload.DetectedPartiesJson)
? JsonSerializer.Deserialize<List<ExtractedParty>>(upload.DetectedPartiesJson) ?? new()
     : new List<ExtractedParty>());

    // 1. Create or find Company records for each party
        var partyCompanyMap = new Dictionary<int, Guid>(); // index -> companyId
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

     // Try to find existing company by name
    var existing = await _db.Companies
           .FirstOrDefaultAsync(c => c.Name == p.Name || c.CuiRo == p.FiscalId);

       if (existing != null)
            {
        partyCompanyMap[i] = existing.Id;
            }
else
        {
         var company = new Company
          {
     Id = Guid.NewGuid(),
    TenantId = tenantId ?? Guid.Empty,
  Name = p.Name,
        CompanyType = companyType,
  CuiRo = p.FiscalId,
           CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
 };
     _db.Companies.Add(company);
       partyCompanyMap[i] = company.Id;
            }
  }

// Find debtor company ID for the case
        var debtorIdx = parties.FindIndex(p => p.Role == "Debtor");
        Guid? debtorCompanyId = debtorIdx >= 0 ? partyCompanyMap[debtorIdx]
   : upload.MatchedCompanyId ?? request.CompanyId;

        // 2. Create the InsolvencyCase
 var newCase = new InsolvencyCase
        {
        Id = Guid.NewGuid(),
      TenantId = tenantId ?? Guid.Empty,
     CaseNumber = caseNumber,
  CourtName = courtName,
            CourtSection = courtSection,
      JudgeSyndic = judgeSyndic,
            DebtorName = debtorName,
            ProcedureType = procedureType,
        Stage = CaseStage.EligibilitySetup,
   LawReference = "Legea 85/2014",
      PractitionerName = parties.FirstOrDefault(p => p.Role == "InsolvencyPractitioner")?.Name,
            PractitionerRole = procedureType is ProcedureType.FalimentSimplificat or ProcedureType.Faliment
           ? "lichidator_judiciar" : "administrator_judiciar",
  CompanyId = debtorCompanyId,
   AssignedToUserId = _currentUser.UserId,
   OpeningDate = openingDate,
         NextHearingDate = nextHearing,
            ClaimsDeadline = claimsDeadline,
       ContestationsDeadline = contestationsDeadline,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
    };
        _db.InsolvencyCases.Add(newCase);

        // 3. Attach the document
        var doc = new InsolvencyDocument
        {
   Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.Empty,
    CaseId = newCase.Id,
        SourceFileName = upload.OriginalFileName,
            DocType = upload.DetectedDocType ?? "unknown",
  UploadedBy = _currentUser.Email ?? "Unknown",
      UploadedAt = upload.UploadedAt,
      RawExtraction = upload.ExtractedText,
      CreatedOn = DateTime.UtcNow,
   CreatedBy = _currentUser.Email ?? "System",
        };
        _db.InsolvencyDocuments.Add(doc);

     // 4. Create CaseParties
        for (var i = 0; i < parties.Count; i++)
        {
        var p = parties[i];
          if (!Enum.TryParse<CasePartyRole>(p.Role, true, out var role))
  continue;

_db.CaseParties.Add(new CaseParty
 {
      Id = Guid.NewGuid(),
          TenantId = tenantId ?? Guid.Empty,
          CaseId = newCase.Id,
       CompanyId = partyCompanyMap[i],
    Role = role,
    ClaimAmountRon = p.ClaimAmount,
                JoinedDate = openingDate,
  CreatedOn = DateTime.UtcNow,
      CreatedBy = _currentUser.Email ?? "System",
            });
}

 // 5. Initialize CasePhases based on procedure type
        var phases = GetPhasesForProcedure(procedureType);
        var sortOrder = 1;
   foreach (var phaseType in phases)
{
            var phase = new CasePhase
   {
          Id = Guid.NewGuid(),
    TenantId = tenantId ?? Guid.Empty,
       CaseId = newCase.Id,
       PhaseType = phaseType,
      Status = sortOrder == 1 ? PhaseStatus.Completed : sortOrder == 2 ? PhaseStatus.InProgress : PhaseStatus.NotStarted,
    SortOrder = sortOrder,
     CreatedOn = DateTime.UtcNow,
              CreatedBy = _currentUser.Email ?? "System",
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

  // 6. Auto-generate tasks
        if (debtorCompanyId.HasValue)
        {
      var tasks = GenerateTasks(newCase, debtorCompanyId.Value, tenantId ?? Guid.Empty,
          openingDate, claimsDeadline, nextHearing);
   _db.CompanyTasks.AddRange(tasks);
        }

        // 7. Schedule reminder emails
        var emails = GenerateReminderEmails(newCase, tenantId ?? Guid.Empty,
   openingDate, claimsDeadline, contestationsDeadline, nextHearing);
        _db.ScheduledEmails.AddRange(emails);

        await _db.SaveChangesAsync();

        // 8. Auto-generate key documents via mail merge (fire-and-forget, don't fail the whole request)
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
        catch (Exception)
        {
     // Log but don't fail the case creation
        }

        await _audit.LogEntityAsync("Case.CreatedFromUpload", "InsolvencyCase", newCase.Id,
  newValues: new
    {
        newCase.CaseNumber, newCase.DebtorName, procedureType = procedureType.ToString(),
       companiesCreated = partyCompanyMap.Count, partiesCreated = parties.Count,
         phasesCreated = phases.Count, emailsScheduled = emails.Count,
      }, severity: "Critical");

        return Ok(new
        {
    action = "newCase",
        caseId = newCase.Id,
documentId = doc.Id,
      companiesCreated = partyCompanyMap.Count,
    partiesCreated = parties.Count,
       phasesCreated = phases.Count,
       tasksCreated = debtorCompanyId.HasValue ? 5 : 0,
      emailsScheduled = emails.Count,
            documentsGenerated = generatedDocs.Count,
      generatedDocuments = generatedDocs.Select(d => new { d.TemplateType, d.FileName, d.StorageKey }).ToList(),
 });
    }

    // ?? Filing to existing case ??????????????????????????

    private async Task<IActionResult> FileToExistingCase(PendingUpload upload, Guid caseId)
    {
        var existingCase = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId);
        if (existingCase == null) return BadRequest(new { message = "Case not found" });

  var doc = new InsolvencyDocument
        {
    Id = Guid.NewGuid(),
            TenantId = existingCase.TenantId,
            CaseId = existingCase.Id,
      SourceFileName = upload.OriginalFileName,
            DocType = upload.DetectedDocType ?? "unknown",
     UploadedBy = _currentUser.Email ?? "Unknown",
        UploadedAt = upload.UploadedAt,
  RawExtraction = upload.ExtractedText,
   CreatedOn = DateTime.UtcNow,
      CreatedBy = _currentUser.Email ?? "System",
        };
_db.InsolvencyDocuments.Add(doc);
        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("Document.FiledToCase", "InsolvencyDocument", doc.Id,
   newValues: new { caseId = existingCase.Id, existingCase.CaseNumber, upload.OriginalFileName, upload.DetectedDocType });

 return Ok(new { action = "filing", caseId = existingCase.Id, documentId = doc.Id });
    }

    // ?? Phase templates ??????????????????????????????????

    private static List<PhaseType> GetPhasesForProcedure(ProcedureType procedure)
    {
        return procedure switch
        {
 ProcedureType.FalimentSimplificat or ProcedureType.Faliment => new()
            {
      PhaseType.OpeningRequest,
     PhaseType.CreditorNotification,
     PhaseType.ClaimsFiling,
         PhaseType.PreliminaryClaimsTable,
             PhaseType.ClaimsContestations,
     PhaseType.DefinitiveClaimsTable,
       PhaseType.AssetLiquidation,
     PhaseType.CreditorDistribution,
        PhaseType.FinalReport,
           PhaseType.ProcedureClosure,
      },
            ProcedureType.Reorganizare => new()
    {
     PhaseType.OpeningRequest,
     PhaseType.ObservationPeriod,
    PhaseType.CreditorNotification,
    PhaseType.ClaimsFiling,
    PhaseType.PreliminaryClaimsTable,
       PhaseType.ClaimsContestations,
           PhaseType.DefinitiveClaimsTable,
   PhaseType.CausesReport,
        PhaseType.ReorganizationPlanProposal,
         PhaseType.ReorganizationPlanVoting,
    PhaseType.ReorganizationPlanConfirmation,
      PhaseType.ReorganizationExecution,
    PhaseType.ProcedureClosure,
 },
            ProcedureType.ConcordatPreventiv => new()
            {
   PhaseType.OpeningRequest,
      PhaseType.ObservationPeriod,
       PhaseType.CreditorNotification,
          PhaseType.ReorganizationPlanProposal,
         PhaseType.ReorganizationPlanVoting,
           PhaseType.ReorganizationPlanConfirmation,
          PhaseType.ProcedureClosure,
            },
      _ => new()
          {
PhaseType.OpeningRequest,
     PhaseType.ObservationPeriod,
    PhaseType.CreditorNotification,
       PhaseType.ClaimsFiling,
     PhaseType.PreliminaryClaimsTable,
                PhaseType.ClaimsContestations,
      PhaseType.DefinitiveClaimsTable,
          PhaseType.FinalReport,
     PhaseType.ProcedureClosure,
   },
        };
    }

    // ?? Auto-generate tasks ??????????????????????????????

 private List<CompanyTask> GenerateTasks(InsolvencyCase cas, Guid companyId, Guid tenantId,
        DateTime openingDate, DateTime? claimsDeadline, DateTime? nextHearing)
    {
    var tasks = new List<CompanyTask>();
        var userId = _currentUser.UserId;
  var email = _currentUser.Email ?? "System";

        tasks.Add(new CompanyTask
  {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId,
            Title = $"Notificare deschidere procedur? - {cas.DebtorName}",
            Description = $"Publicare în BPI ?i notificare ONRC pentru dosarul {cas.CaseNumber}",
      Labels = "notification, urgent",
            Deadline = openingDate.AddDays(3),
        Status = Domain.Enums.TaskStatus.Open,
    AssignedToUserId = userId,
   CreatedOn = DateTime.UtcNow, CreatedBy = email,
    });

        tasks.Add(new CompanyTask
     {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId,
            Title = $"Notificare creditori cunoscu?i - {cas.DebtorName}",
  Description = "Trimite notific?ri c?tre to?i creditorii identifica?i din documentele primite",
        Labels = "notification, creditors",
     Deadline = openingDate.AddDays(5),
       Status = Domain.Enums.TaskStatus.Open,
          AssignedToUserId = userId,
        CreatedOn = DateTime.UtcNow, CreatedBy = email,
        });

   tasks.Add(new CompanyTask
  {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId,
            Title = $"Raport Art. 97 - {cas.DebtorName}",
   Description = $"Întocmire raport privind cauzele ?i împrejur?rile care au dus la apari?ia st?rii de insolven?? - dosar {cas.CaseNumber}",
     Labels = "report",
  Deadline = openingDate.AddDays(40),
            Status = Domain.Enums.TaskStatus.Open,
   AssignedToUserId = userId,
            CreatedOn = DateTime.UtcNow, CreatedBy = email,
        });

        if (claimsDeadline.HasValue)
 {
            tasks.Add(new CompanyTask
            {
      Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId,
    Title = $"Verificare ?i întocmire tabel preliminar crean?e - {cas.DebtorName}",
      Description = $"Termen crean?e: {claimsDeadline.Value:dd.MM.yyyy}. Verificare declara?ii de crean?? primite.",
                Labels = "claims, deadline",
    Deadline = claimsDeadline.Value.AddDays(5),
   Status = Domain.Enums.TaskStatus.Open,
      AssignedToUserId = userId,
    CreatedOn = DateTime.UtcNow, CreatedBy = email,
            });
        }

        if (nextHearing.HasValue)
 {
            tasks.Add(new CompanyTask
        {
         Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId,
     Title = $"Preg?tire termen de judecat? - {cas.DebtorName}",
      Description = $"Termen: {nextHearing.Value:dd.MM.yyyy} la {cas.CourtName}. Preg?tire rapoarte ?i documente.",
     Labels = "hearing, court",
    Deadline = nextHearing.Value.AddDays(-2),
   Status = Domain.Enums.TaskStatus.Open,
 AssignedToUserId = userId,
          CreatedOn = DateTime.UtcNow, CreatedBy = email,
  });
        }

    return tasks;
    }

    // ?? Auto-schedule reminder emails ????????????????????

    private List<ScheduledEmail> GenerateReminderEmails(InsolvencyCase cas, Guid tenantId,
 DateTime openingDate, DateTime? claimsDeadline, DateTime? contestationsDeadline, DateTime? nextHearing)
  {
    var emails = new List<ScheduledEmail>();
        var recipient = _currentUser.Email ?? "practitioner@insolvex.local";
        var now = DateTime.UtcNow;

        // Claims deadline reminder (7 days before, 1 day before)
        if (claimsDeadline.HasValue)
        {
            var sevenBefore = claimsDeadline.Value.AddDays(-7);
         if (sevenBefore > now)
      {
      emails.Add(new ScheduledEmail
            {
      Id = Guid.NewGuid(), TenantId = tenantId,
         To = recipient,
       Subject = $"[Insolvex] Termen crean?e în 7 zile - {cas.DebtorName} ({cas.CaseNumber})",
    Body = $"Termenul limit? pentru depunerea crean?elor în dosarul {cas.CaseNumber} ({cas.DebtorName}) este {claimsDeadline.Value:dd.MM.yyyy}.\n\nMai ave?i 7 zile.",
    ScheduledFor = sevenBefore,
       CreatedOn = now, CreatedBy = "System",
  });
      }

      var oneBefore = claimsDeadline.Value.AddDays(-1);
            if (oneBefore > now)
         {
        emails.Add(new ScheduledEmail
   {
     Id = Guid.NewGuid(), TenantId = tenantId,
      To = recipient,
     Subject = $"[Insolvex] URGENT: Termen crean?e MÂINE - {cas.DebtorName}",
                Body = $"ATEN?IE: Termenul pentru crean?e în dosarul {cas.CaseNumber} expir? MÂINE {claimsDeadline.Value:dd.MM.yyyy}.",
 ScheduledFor = oneBefore,
      CreatedOn = now, CreatedBy = "System",
            });
   }
     }

        // Hearing reminder (2 days before)
        if (nextHearing.HasValue)
        {
            var twoBefore = nextHearing.Value.AddDays(-2);
  if (twoBefore > now)
            {
    emails.Add(new ScheduledEmail
         {
          Id = Guid.NewGuid(), TenantId = tenantId,
             To = recipient,
        Subject = $"[Insolvex] Termen de judecat? în 2 zile - {cas.DebtorName}",
   Body = $"Dosarul {cas.CaseNumber}: termen la {cas.CourtName} pe {nextHearing.Value:dd.MM.yyyy}.\n\nPreg?ti?i documentele.",
              ScheduledFor = twoBefore,
      CreatedOn = now, CreatedBy = "System",
                });
}
        }

  // Contestations reminder
        if (contestationsDeadline.HasValue)
        {
var threeBefore = contestationsDeadline.Value.AddDays(-3);
            if (threeBefore > now)
  {
              emails.Add(new ScheduledEmail
    {
 Id = Guid.NewGuid(), TenantId = tenantId,
          To = recipient,
   Subject = $"[Insolvex] Termen contesta?ii în 3 zile - {cas.DebtorName}",
        Body = $"Termenul pentru contesta?ii tabel crean?e: {contestationsDeadline.Value:dd.MM.yyyy}.",
        ScheduledFor = threeBefore,
       CreatedOn = now, CreatedBy = "System",
     });
   }
        }

  return emails;
    }
}

// ?? Request / Response models ????????????????????????????

public record ConfirmUploadRequest(
    string Action,
    string? CaseNumber = null,
    string? CourtName = null,
    string? CourtSection = null,
    string? DebtorName = null,
    string? JudgeSyndic = null,
    string? ProcedureType = null,
    DateTime? OpeningDate = null,
    DateTime? NextHearingDate = null,
    DateTime? ClaimsDeadline = null,
  DateTime? ContestationsDeadline = null,
    Guid? CompanyId = null,
 Guid? CaseId = null,
    List<ExtractedParty>? Parties = null
);

public class UploadResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? RecommendedAction { get; set; }
    public string? DocType { get; set; }
    public string? CaseNumber { get; set; }
    public string? DebtorName { get; set; }
    public string? CourtName { get; set; }
    public string? CourtSection { get; set; }
    public string? JudgeSyndic { get; set; }
  public Guid? MatchedCaseId { get; set; }
    public Guid? MatchedCompanyId { get; set; }
    public double Confidence { get; set; }
    public string? ProcedureType { get; set; }
    public DateTime? OpeningDate { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public DateTime? ClaimsDeadline { get; set; }
    public DateTime? ContestationsDeadline { get; set; }
    public List<ExtractedParty> Parties { get; set; } = new();
    public string? ExtractedText { get; set; }
}
