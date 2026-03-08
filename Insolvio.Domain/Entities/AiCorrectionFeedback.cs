namespace Insolvio.Domain.Entities;

/// <summary>
/// Records every AI field suggestion that a user accepted or corrected.
/// Each row represents one field from one document processing event.
/// Anonymised (TenantIdHash instead of raw TenantId) so the global
/// feedback store can drive model retraining without leaking tenant identity.
/// </summary>
public class AiCorrectionFeedback : BaseEntity
{
    /// <summary>Logical document type key, e.g. "CourtOpeningDecision".</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Field that was extracted, e.g. "CaseNumber", "DebtorName".</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The value the AI model originally suggested.</summary>
    public string AiSuggestedValue { get; set; } = string.Empty;

    /// <summary>The value the user actually saved (may equal AiSuggestedValue).</summary>
    public string UserCorrectedValue { get; set; } = string.Empty;

    /// <summary><c>true</c> when the user kept the AI value unchanged.</summary>
    public bool WasAccepted { get; set; }

    /// <summary>Confidence score the model reported for this field (0–1).</summary>
    public float? AiConfidence { get; set; }

    /// <summary>
    /// ~200 chars of surrounding document text for context. Sent only from
    /// the annotation modal; omitted when context would contain PII.
    /// </summary>
    public string DocumentTextSnippet { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the tenant ID — links corrections per tenant
    /// without exposing the identity in the global feedback store.
    /// </summary>
    public string TenantIdHash { get; set; } = string.Empty;

    /// <summary>When the user made the correction.</summary>
    public DateTime CorrectedAt { get; set; }

    /// <summary>
    /// Which UI surface captured this correction.
    /// Values: "annotation_modal", "case_creation", "document_review".
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
