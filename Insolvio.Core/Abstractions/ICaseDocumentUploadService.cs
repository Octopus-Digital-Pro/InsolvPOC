namespace Insolvio.Core.Abstractions;

/// <summary>
/// Direct document upload to an existing case.
///
/// Unlike <see cref="IDocumentUploadService"/> (which runs AI classification
/// and creates a PendingUpload for review), this service accepts an
/// <em>explicit</em> document type from the caller, stores the file
/// under the canonical <c>cases/{caseId}/{docType}/{docId}{ext}</c> path,
/// applies any saved IncomingDocumentProfile annotations as AI prompt
/// context, then runs AI field extraction and persists the result.
/// </summary>
public interface ICaseDocumentUploadService
{
    /// <summary>
    /// Uploads a file for a case document type, stores it in the canonical
    /// folder, extracts text, runs AI analysis with annotation context,
    /// and saves an <c>InsolvencyDocument</c> record.
    /// </summary>
    Task<CaseDocumentUploadResult> UploadAsync(
        Guid caseId,
        string docType,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Creates all standard sub-folders for a case in the configured
    /// storage backend. Safe to call multiple times (idempotent).
    /// </summary>
    Task EnsureCaseFolderStructureAsync(Guid caseId, CancellationToken ct = default);
}

// ── Result ────────────────────────────────────────────────────────────────────

public class CaseDocumentUploadResult
{
    public Guid DocumentId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public string DocType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    /// <summary>Whether AI field extraction ran and produced results.</summary>
    public bool AiExtracted { get; init; }
    /// <summary>AI-generated summary of the document content.</summary>
    public string? AiSummary { get; init; }
    /// <summary>Whether annotations from the IncomingDocumentProfile were applied.</summary>
    public bool AnnotationsApplied { get; init; }
    /// <summary>Confidence score (0–1) from AI extraction.</summary>
    public double? AiConfidence { get; init; }
    /// <summary>JSON of all structured fields extracted by AI.</summary>
    public string? FieldsExtractedJson { get; init; }
}
