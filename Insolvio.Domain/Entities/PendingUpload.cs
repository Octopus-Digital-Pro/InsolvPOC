using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Temporary record for an uploaded document pending AI classification
/// and user confirmation before being filed into a case.
/// </summary>
public class PendingUpload : BaseEntity
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByEmail { get; set; }
    public Guid? TenantId { get; set; }

    // AI Classification results
    /// <summary>newCase or filing</summary>
    public string? RecommendedAction { get; set; }
    public string? DetectedDocType { get; set; }
    public string? DetectedCaseNumber { get; set; }
    public string? DetectedDebtorName { get; set; }
    public string? DetectedCourtName { get; set; }
    public Guid? MatchedCaseId { get; set; }
    public Guid? MatchedCompanyId { get; set; }
    public string? ExtractedText { get; set; }
    public double Confidence { get; set; }

    // Structured extraction (new)
    public ProcedureType? DetectedProcedureType { get; set; }
    public string? DetectedCourtSection { get; set; }
    public string? DetectedJudgeSyndic { get; set; }
    public string? DetectedRegistrar { get; set; }
    public DateTime? DetectedOpeningDate { get; set; }
    public DateTime? DetectedNextHearingDate { get; set; }
    public DateTime? DetectedClaimsDeadline { get; set; }
    public DateTime? DetectedContestationsDeadline { get; set; }
    /// <summary>JSON array of ExtractedParty objects</summary>
    public string? DetectedPartiesJson { get; set; }
    /// <summary>Romanian CUI of the debtor company, e.g. "RO12345678"</summary>
    public string? DetectedDebtorCui { get; set; }
    /// <summary>True when classification was performed by the AI provider (vs regex heuristics)</summary>
    public bool IsAiExtracted { get; set; }
}
