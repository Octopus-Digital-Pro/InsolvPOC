using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for document upload lifecycle:
///   - Classify an uploaded file using AI extraction
///   - Store the pending upload for user review
///   - Confirm the upload (creates a new case or files to an existing one)
///   - Retrieve pending upload details
///
/// All operations are tenant-scoped and fully audited.
/// </summary>
public interface IDocumentUploadService
{
    /// <summary>
    /// Accept a file upload, classify it using AI extraction,
    /// and persist a PendingUpload record for user review.
    /// Audit: "A document was uploaded and classified by AI."
    /// </summary>
    Task<DocumentUploadResult> ClassifyAndStoreUploadAsync(
        DocumentUploadRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve a previously uploaded document's classification details.
    /// </summary>
    Task<DocumentUploadResult?> GetPendingUploadAsync(
        Guid uploadId,
        CancellationToken ct = default);

    /// <summary>
    /// Confirm a pending upload: either create a full new case
    /// (with parties, phases, tasks, emails, documents) or file it
    /// into an existing case.
    /// Audit: "A pending upload was confirmed and a new insolvency case was created."
    /// or "A document was filed into an existing insolvency case."
    /// </summary>
    Task<UploadConfirmationResult> ConfirmUploadAsync(
        Guid uploadId,
        ConfirmUploadCommand command,
        CancellationToken ct = default);
}

// ?? Value objects / commands ????????????????????????????

/// <summary>
/// Input for an upload operation.
/// </summary>
public class DocumentUploadRequest
{
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required string ContentType { get; init; }
    public required Stream FileStream { get; init; }
}

/// <summary>
/// Result returned after AI classification of an upload.
/// </summary>
public class DocumentUploadResult
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? RecommendedAction { get; init; }
    public string? DocType { get; init; }
    public string? CaseNumber { get; init; }
    public string? DebtorName { get; init; }
    public string? CourtName { get; init; }
    public string? CourtSection { get; init; }
    public string? JudgeSyndic { get; init; }
    public Guid? MatchedCaseId { get; init; }
    public Guid? MatchedCompanyId { get; init; }
    public double Confidence { get; init; }
    public string? ProcedureType { get; init; }
    public DateTime? OpeningDate { get; init; }
    public DateTime? NextHearingDate { get; init; }
    public DateTime? ClaimsDeadline { get; init; }
    public DateTime? ContestationsDeadline { get; init; }
    public List<ExtractedPartyResult> Parties { get; init; } = new();
    public string? ExtractedText { get; init; }
}

public class ExtractedPartyResult
{
    public string Role { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? FiscalId { get; init; }
    public decimal? ClaimAmount { get; init; }
}

/// <summary>
/// Command to confirm a pending upload.
/// Action: "newCase" creates a full case; "filing" files into an existing case.
/// </summary>
public class ConfirmUploadCommand
{
    public required string Action { get; init; }
    public string? CaseNumber { get; init; }
    public string? CourtName { get; init; }
    public string? CourtSection { get; init; }
    public string? DebtorName { get; init; }
    public string? JudgeSyndic { get; init; }
    public string? ProcedureType { get; init; }
    public DateTime? OpeningDate { get; init; }
    public DateTime? NextHearingDate { get; init; }
    public DateTime? ClaimsDeadline { get; init; }
    public DateTime? ContestationsDeadline { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? CaseId { get; init; }
    public List<ExtractedPartyResult>? Parties { get; init; }
}

/// <summary>
/// Result of confirming a pending upload.
/// </summary>
public class UploadConfirmationResult
{
    public required string Action { get; init; }
    public Guid CaseId { get; init; }
    public Guid DocumentId { get; init; }
    public string? CaseNumber { get; init; }
    public int CompaniesCreated { get; init; }
    public int PartiesCreated { get; init; }
    public int PhasesCreated { get; init; }
    public int TasksCreated { get; init; }
    public int EmailsScheduled { get; init; }
    public int DocumentsGenerated { get; init; }
    public List<GeneratedDocSummary> GeneratedDocuments { get; init; } = new();
}

public class GeneratedDocSummary
{
    public string TemplateType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
}
