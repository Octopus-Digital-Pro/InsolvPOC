namespace Insolvex.Domain.Entities;

/// <summary>
/// Stores everything known about a reference incoming document type:
/// the storage key for the sample PDF, the visual field annotations drawn
/// in the PDF annotator tool, and AI-generated descriptions/parameter
/// summaries in all three supported UI languages (EN / RO / HU).
///
/// One row per (TenantId, DocumentType) — upserted whenever the admin
/// uploads a new sample PDF or saves annotations.  The AI analysis
/// is triggered explicitly and can be re-run at any time.
///
/// These records form the per-tenant document-shape database that the
/// case-level document matching logic compares incoming uploaded documents
/// against when performing auto-classification.
/// </summary>
public class IncomingDocumentProfile : TenantScopedEntity
{
    /// <summary>
    /// Logical document type key (e.g. "CourtOpeningDecision").
    /// Must match the <c>IncomingDocumentType</c> values understood by the frontend.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    // ── Reference PDF ─────────────────────────────────────────────────────────

    /// <summary>Blob / file-storage key for the uploaded reference PDF.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Original file name of the uploaded sample (for display).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Size of the uploaded PDF in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>When the reference PDF was last uploaded / replaced.</summary>
    public DateTime UploadedOn { get; set; }

    // ── Annotations ───────────────────────────────────────────────────────────

    /// <summary>
    /// JSON array of <c>IncomingAnnotationItem</c> rectangles drawn over the
    /// reference PDF to identify where each insolvency field appears.
    /// Stored as relative (0–1) coordinates so they scale to any render size.
    /// </summary>
    public string? AnnotationsJson { get; set; }

    /// <summary>Free-text admin notes about this document type (layout, variants, etc.).</summary>
    public string? AnnotationNotes { get; set; }

    /// <summary>When the annotations were last saved.</summary>
    public DateTime? LastAnnotatedOn { get; set; }

    // ── AI analysis ───────────────────────────────────────────────────────────

    /// <summary>
    /// AI-generated natural-language description of this document type in English.
    /// Describes the document purpose, typical structure, and which fields are present.
    /// </summary>
    public string? AiSummaryEn { get; set; }

    /// <summary>AI-generated description in Romanian.</summary>
    public string? AiSummaryRo { get; set; }

    /// <summary>AI-generated description in Hungarian.</summary>
    public string? AiSummaryHu { get; set; }

    /// <summary>
    /// JSON object describing key structural parameters extracted by AI:
    /// typical field locations, expected data formats, distinguishing features
    /// that allow automatic recognition.
    /// Schema: { fieldParameters: [...], layoutFeatures: [...], matchingRules: [...] }
    /// </summary>
    public string? AiParametersJson { get; set; }

    /// <summary>The AI provider + model used for the last analysis run.</summary>
    public string? AiModel { get; set; }

    /// <summary>Confidence score (0–1) from the last AI analysis.</summary>
    public double? AiConfidence { get; set; }

    /// <summary>When the last AI analysis was performed.</summary>
    public DateTime? AiAnalysedOn { get; set; }

    // ── Status ───────────────────────────────────────────────────────────────

    /// <summary>Whether this profile is the active reference for matching.</summary>
    public bool IsActive { get; set; } = true;
}
