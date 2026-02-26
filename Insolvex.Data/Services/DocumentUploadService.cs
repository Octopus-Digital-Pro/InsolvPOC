using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

/// <summary>
/// Implements <see cref="IDocumentUploadService"/>: orchestrates the full
/// document upload lifecycle from classification through case creation.
///
/// Domain language:
///   - "classify"  ? AI extraction + matching against existing cases/companies
///   - "store"     ? persist PendingUpload for user review
///   - "confirm"   ? create a new case (via CaseCreationService) or file to existing
///
/// All operations are tenant-scoped via ICurrentUserService.TenantId.
/// All mutations are audited with human-readable descriptions.
/// </summary>
public sealed class DocumentUploadService : IDocumentUploadService
{
  private readonly ApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;
  private readonly IAuditService _audit;
  private readonly DocumentClassificationService _classifier;
  private readonly CaseCreationService _caseCreation;
  private readonly SummaryRefreshService _summaryRefresh;
  private readonly ICaseEventService _caseEvents;
  private readonly ILogger<DocumentUploadService> _logger;

  private static readonly string[] AllowedExtensions =
        [".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".tiff"];

  public DocumentUploadService(
      ApplicationDbContext db,
      ICurrentUserService currentUser,
      IAuditService audit,
      DocumentClassificationService classifier,
      CaseCreationService caseCreation,
      SummaryRefreshService summaryRefresh,
      ICaseEventService caseEvents,
      ILogger<DocumentUploadService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _audit = audit;
    _classifier = classifier;
    _caseCreation = caseCreation;
    _summaryRefresh = summaryRefresh;
    _caseEvents = caseEvents;
    _logger = logger;
  }

  // ?? Upload + Classify ???????????????????????????????????

  /// <inheritdoc />
  public async Task<DocumentUploadResult> ClassifyAndStoreUploadAsync(
DocumentUploadRequest request, CancellationToken ct)
  {
    ValidateUploadRequest(request);

    var tenantId = RequireTenantId();
    var ext = Path.GetExtension(request.FileName).ToLowerInvariant();

    // 1. Persist the file to a temporary location
    var storedName = $"{Guid.NewGuid()}{ext}";
    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "TempUploads");
    Directory.CreateDirectory(uploadsDir);
    var filePath = Path.Combine(uploadsDir, storedName);

    await using (var fileStream = new FileStream(filePath, FileMode.Create))
    {
      await request.FileStream.CopyToAsync(fileStream, ct);
    }

    // 2. Run AI classification
    var classification = await _classifier.ClassifyAsync(filePath, request.FileName);

    // 3. Store PendingUpload entity (tenant-scoped)
    var upload = new PendingUpload
    {
      Id = Guid.NewGuid(),
      OriginalFileName = request.FileName,
      StoredFileName = storedName,
      FilePath = filePath,
      FileSize = request.FileSize,
      ContentType = request.ContentType,
      UploadedAt = DateTime.UtcNow,
      UploadedByUserId = _currentUser.UserId,
      UploadedByEmail = _currentUser.Email,
      TenantId = tenantId,
      // AI classification results
      RecommendedAction = classification.RecommendedAction,
      DetectedDocType = classification.DocType,
      DetectedCaseNumber = classification.CaseNumber,
      DetectedDebtorName = classification.DebtorName,
      DetectedCourtName = classification.CourtName,
      MatchedCaseId = classification.MatchedCaseId,
      MatchedCompanyId = classification.MatchedCompanyId,
      ExtractedText = classification.ExtractedText,
      Confidence = classification.Confidence,
      DetectedProcedureType = classification.DetectedProcedureType,
      DetectedCourtSection = classification.CourtSection,
      DetectedJudgeSyndic = classification.JudgeSyndic,
      DetectedOpeningDate = classification.OpeningDate,
      DetectedNextHearingDate = classification.NextHearingDate,
      DetectedClaimsDeadline = classification.ClaimsDeadline,
      DetectedContestationsDeadline = classification.ContestationsDeadline,
      DetectedPartiesJson = classification.Parties.Count > 0
    ? JsonSerializer.Serialize(classification.Parties)
      : null,
      DetectedDebtorCui = classification.DebtorCui,
      IsAiExtracted = classification.IsAiExtracted,
    };

    _db.Set<PendingUpload>().Add(upload);
    await _db.SaveChangesAsync(ct);

    // 4. Audit: human-readable description
    await _audit.LogAsync(new AuditEntry
    {
      Action = "Document Uploaded and Classified by AI",
      Description = $"A document '{request.FileName}' was uploaded and classified by AI as '{classification.DocType ?? "unknown"}' with {classification.Confidence:P0} confidence.",
      EntityType = "PendingUpload",
      EntityId = upload.Id,
      EntityName = request.FileName,
      CaseNumber = classification.CaseNumber,
      NewValues = new
      {
        upload.OriginalFileName,
        upload.DetectedDocType,
        upload.DetectedCaseNumber,
        upload.Confidence,
        upload.RecommendedAction,
      },
      Severity = "Info",
      Category = "Document",
    });

    _logger.LogInformation(
        "Document '{FileName}' uploaded and classified as '{DocType}' (confidence: {Confidence:P0}) for tenant {TenantId}",
        request.FileName, classification.DocType, classification.Confidence, tenantId);

    return MapToResult(upload, classification.Parties);
  }

  // ?? Retrieve Pending Upload ?????????????????????????????

  /// <inheritdoc />
  public async Task<DocumentUploadResult?> GetPendingUploadAsync(
      Guid uploadId, CancellationToken ct)
  {
    var tenantId = RequireTenantId();

    var upload = await _db.Set<PendingUpload>()
    .FirstOrDefaultAsync(x => x.Id == uploadId && x.TenantId == tenantId, ct);

    if (upload is null) return null;

    var parties = DeserializeParties(upload.DetectedPartiesJson);
    return MapToResult(upload, parties);
  }

  // ?? Confirm Upload ??????????????????????????????????????

  /// <inheritdoc />
  public async Task<UploadConfirmationResult> ConfirmUploadAsync(
        Guid uploadId, ConfirmUploadCommand command, CancellationToken ct)
  {
    var tenantId = RequireTenantId();

    var upload = await _db.Set<PendingUpload>()
.FirstOrDefaultAsync(x => x.Id == uploadId && x.TenantId == tenantId, ct);

    if (upload is null)
      throw new BusinessException($"Pending upload {uploadId} not found for this tenant.");

    return command.Action == "filing" && command.CaseId.HasValue
        ? await FileDocumentToExistingCaseAsync(upload, command.CaseId.Value, ct)
: await CreateNewCaseFromUploadAsync(upload, command, ct);
  }

  // ?? Private: Create New Case ????????????????????????????

  private async Task<UploadConfirmationResult> CreateNewCaseFromUploadAsync(
      PendingUpload upload, ConfirmUploadCommand command, CancellationToken ct)
  {
    // Map the command parties to the CaseCreationService's expected format
    var parties = command.Parties?.Select(p => new CaseCreationParty
    {
      Role = p.Role,
      Name = p.Name,
      FiscalId = p.FiscalId,
      ClaimAmount = p.ClaimAmount,
    }).ToList();

    // Build the CaseCreationRequest, merging command overrides
    var creationRequest = new CaseCreationRequest
    {
      CaseNumber = command.CaseNumber,
      CourtName = command.CourtName,
      CourtSection = command.CourtSection,
      DebtorName = command.DebtorName,
      DebtorCui = command.DebtorCui,
      JudgeSyndic = command.JudgeSyndic,
      ProcedureType = command.ProcedureType,
      NoticeDate = command.OpeningDate,
      OpeningDate = command.OpeningDate,
      NextHearingDate = command.NextHearingDate,
      ClaimsDeadline = command.ClaimsDeadline,
      ContestationsDeadline = command.ContestationsDeadline,
      CompanyId = command.CompanyId,
      Parties = parties,
    };

    // Delegate the entire orchestration to CaseCreationService
    var result = await _caseCreation.CreateFromUploadAsync(upload, creationRequest);

    // Audit: ubiquitous language
    await _audit.LogAsync(new AuditEntry
    {
      Action = "Case.CreatedFromUpload",
      Description = $"A new insolvency case '{result.CaseNumber}' was created from an uploaded document. " +
          $"{result.CompaniesCreated} companies registered, {result.PartiesCreated} parties linked, " +
              $"{result.TasksCreated} tasks auto-generated, " +
      $"{result.EmailsScheduled} reminder emails scheduled.",
      EntityType = "InsolvencyCase",
      EntityId = result.CaseId,
      EntityName = result.CaseNumber,
      CaseNumber = result.CaseNumber,
      NewValues = new
      {
        result.CaseNumber,
        result.NoticeDate,
        Status = result.Status,
        result.CompaniesCreated,
        result.PartiesCreated,
        result.TasksCreated,
        result.EmailsScheduled,
        result.DocumentsGenerated,
      },
      Severity = "Critical",
      Category = "CaseCreation",
    });

    _logger.LogInformation(
              "Case '{CaseNumber}' created from upload {UploadId} — {Parties} parties, {Tasks} tasks, {Emails} emails",
    result.CaseNumber, upload.Id, result.PartiesCreated, result.TasksCreated, result.EmailsScheduled);

    // Record timeline event for case creation from uploaded document
    _ = _caseEvents.RecordDocumentUploadedAsync(
      caseId: result.CaseId,
      documentId: result.DocumentId,
      fileName: upload.OriginalFileName,
      docType: upload.DetectedDocType ?? "unknown",
      aiSummary: null,
      extractedParties: upload.DetectedPartiesJson != null
        ? JsonSerializer.Deserialize<object>(upload.DetectedPartiesJson) : null,
      extractedDates: null,
      extractedActions: null,
      ct: default);

    return new UploadConfirmationResult
    {
      Action = "newCase",
      CaseId = result.CaseId,
      DocumentId = result.DocumentId,
      CaseNumber = result.CaseNumber,
      CompaniesCreated = result.CompaniesCreated,
      PartiesCreated = result.PartiesCreated,
      TasksCreated = result.TasksCreated,
      EmailsScheduled = result.EmailsScheduled,
      DocumentsGenerated = result.DocumentsGenerated,
      GeneratedDocuments = result.GeneratedDocuments.Select(d => new GeneratedDocSummary
      {
        TemplateType = d.TemplateType,
        FileName = d.FileName,
        StorageKey = d.StorageKey,
      }).ToList(),
    };
  }

  // ?? Private: File to Existing Case ??????????????????????

  private async Task<UploadConfirmationResult> FileDocumentToExistingCaseAsync(
      PendingUpload upload, Guid caseId, CancellationToken ct)
  {
    var tenantId = RequireTenantId();

    var existingCase = await _db.InsolvencyCases
             .FirstOrDefaultAsync(c => c.Id == caseId && c.TenantId == tenantId, ct);

    if (existingCase is null)
      throw new BusinessException($"Case {caseId} not found for this tenant.");

    var documentSummaries = LocalizedSummaryBuilder.BuildDocumentSummaryByLanguage(
      upload.OriginalFileName,
      upload.DetectedDocType ?? "unknown",
      existingCase.CaseNumber,
      existingCase.DebtorName);

    var document = new InsolvencyDocument
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      CaseId = existingCase.Id,
      SourceFileName = upload.OriginalFileName,
      DocType = upload.DetectedDocType ?? "unknown",
      UploadedBy = _currentUser.Email ?? "Unknown",
      UploadedAt = upload.UploadedAt,
      RawExtraction = upload.ExtractedText,
      Summary = documentSummaries["en"],
      SummaryByLanguageJson = JsonSerializer.Serialize(documentSummaries),
      CreatedOn = DateTime.UtcNow,
      CreatedBy = _currentUser.Email ?? "System",
    };

    _db.InsolvencyDocuments.Add(document);
    await _db.SaveChangesAsync(ct);

    // Audit: ubiquitous language
    await _audit.LogAsync(new AuditEntry
    {
      Action = "Document.FiledToExistingCase",
      Description = $"The document '{upload.OriginalFileName}' was filed into case '{existingCase.CaseNumber}' ({existingCase.DebtorName}).",
      EntityType = "InsolvencyDocument",
      EntityId = document.Id,
      EntityName = upload.OriginalFileName,
      CaseNumber = existingCase.CaseNumber,
      NewValues = new
      {
        CaseId = existingCase.Id,
        existingCase.CaseNumber,
        upload.OriginalFileName,
        upload.DetectedDocType,
      },
      Severity = "Info",
      Category = "Document",
    });

    _logger.LogInformation(
   "Document '{FileName}' filed to case '{CaseNumber}' ({CaseId})",
 upload.OriginalFileName, existingCase.CaseNumber, existingCase.Id);

    // Record timeline event for filed document
    _ = _caseEvents.RecordDocumentUploadedAsync(
      caseId: existingCase.Id,
      documentId: document.Id,
      fileName: upload.OriginalFileName,
      docType: upload.DetectedDocType ?? "unknown",
      aiSummary: null,
      extractedParties: upload.DetectedPartiesJson != null
        ? JsonSerializer.Deserialize<object>(upload.DetectedPartiesJson) : null,
      extractedDates: null,
      extractedActions: null,
      ct: default);

    // Trigger background AI summary refresh after new document
    _ = _summaryRefresh.RefreshIfStaleAsync(
      existingCase.Id, existingCase.TenantId, "document_upload");

    return new UploadConfirmationResult
    {
      Action = "filing",
      CaseId = existingCase.Id,
      DocumentId = document.Id,
      CaseNumber = existingCase.CaseNumber,
    };
  }

  // ?? Validation ??????????????????????????????????????????

  private static void ValidateUploadRequest(DocumentUploadRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.FileName))
      throw new BusinessException("File name is required.");

    if (request.FileSize <= 0)
      throw new BusinessException("File is empty.");

    var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
    if (!AllowedExtensions.Contains(ext))
      throw new BusinessException($"File type '{ext}' is not supported. Allowed: {string.Join(", ", AllowedExtensions)}");
  }

  private Guid RequireTenantId()
  {
    return _currentUser.TenantId
?? throw new BusinessException("Tenant context is required for document operations.");
  }

  // ?? Mapping ?????????????????????????????????????????????

  private static DocumentUploadResult MapToResult(PendingUpload upload, List<ClassificationExtractedParty>? classificationParties)
  {
    var parties = classificationParties?.Select(p => new ExtractedPartyResult
    {
      Role = p.Role,
      Name = p.Name,
      FiscalId = p.FiscalId,
      ClaimAmount = p.ClaimAmount,
    }).ToList() ?? new();

    return MapToResult(upload, parties);
  }

  private static DocumentUploadResult MapToResult(PendingUpload upload, List<ExtractedPartyResult> parties)
  {
    return new DocumentUploadResult
    {
      Id = upload.Id,
      FileName = upload.OriginalFileName,
      FileSize = upload.FileSize,
      RecommendedAction = upload.RecommendedAction,
      DocType = upload.DetectedDocType,
      CaseNumber = upload.DetectedCaseNumber,
      DebtorName = upload.DetectedDebtorName,
      CourtName = upload.DetectedCourtName,
      CourtSection = upload.DetectedCourtSection,
      JudgeSyndic = upload.DetectedJudgeSyndic,
      MatchedCaseId = upload.MatchedCaseId,
      MatchedCompanyId = upload.MatchedCompanyId,
      Confidence = upload.Confidence,
      ProcedureType = upload.DetectedProcedureType?.ToString(),
      OpeningDate = upload.DetectedOpeningDate,
      NextHearingDate = upload.DetectedNextHearingDate,
      ClaimsDeadline = upload.DetectedClaimsDeadline,
      ContestationsDeadline = upload.DetectedContestationsDeadline,
      Parties = parties,
      ExtractedText = upload.ExtractedText,
      DebtorCui = upload.DetectedDebtorCui,
      IsAiExtracted = upload.IsAiExtracted,
    };
  }

  private static List<ExtractedPartyResult> DeserializeParties(string? json)
  {
    if (string.IsNullOrEmpty(json)) return new();

    try
    {
      var raw = JsonSerializer.Deserialize<List<ClassificationExtractedParty>>(json);
      return raw?.Select(p => new ExtractedPartyResult
      {
        Role = p.Role,
        Name = p.Name,
        FiscalId = p.FiscalId,
        ClaimAmount = p.ClaimAmount,
      }).ToList() ?? new();
    }
    catch
    {
      return new();
    }
  }
}
