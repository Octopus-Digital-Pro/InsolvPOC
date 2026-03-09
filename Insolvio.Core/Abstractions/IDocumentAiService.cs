using System.Text.Json;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Unified AI service for document analysis.
/// Covers both text-based structured extraction (used during upload classification)
/// and image-based rich extraction (used in the document review page).
/// Config resolution: tenant API key → global API key fallback.
/// </summary>
public interface IDocumentAiService
{
    /// <summary>
    /// Analyse extracted document text and return structured insolvency case fields.
    /// Returns <c>null</c> when AI is unavailable, disabled, or the call fails.
    /// </summary>
    Task<AiDocumentTextResult?> AnalyzeTextAsync(
        string extractedText,
        string fileName,
        string? annotationContext = null,
        CancellationToken ct = default);

    /// <summary>
    /// Submit base64 document page images to the AI provider and return the rich
    /// structured extraction JSON consumed by the document review page.
    /// Returns <c>null</c> when AI is unavailable, disabled, or the call fails.
    /// </summary>
    Task<JsonElement?> ExtractFromImagesAsync(
        IReadOnlyList<string> base64Images,
        CancellationToken ct = default);

    /// <summary>
    /// Ask the AI to locate verbatim text for each annotatable field within the
    /// extracted document text. Returns a mapping of field name → exact verbatim
    /// substring as it appears in the document.
    /// Returns <c>null</c> when AI is not configured or no API key is available.
    /// Returns an <see cref="AnnotationSuggestResult"/> with <c>CallFailed = true</c>
    /// when AI is configured but the API call itself failed (e.g. wrong key, rate limit).
    /// </summary>
    Task<AnnotationSuggestResult?> SuggestAnnotationsAsync(
        string extractedText,
        CancellationToken ct = default);
}

/// <summary>
/// Holds the outcome of an annotation-suggestion AI call.
/// </summary>
/// <param name="Suggestions">Field name → verbatim text map (may be empty).</param>
/// <param name="CallFailed">
/// <c>true</c> when AI is configured but the underlying API call failed;
/// <c>false</c> when the call succeeded (even if no fields were identified).
/// </param>
public sealed record AnnotationSuggestResult(
    Dictionary<string, string> Suggestions,
    bool CallFailed);

// ── Result types ─────────────────────────────────────────────────────────────

public sealed record AiDocumentTextResult(
    string? DocType,
    string? CaseNumber,
    string? DebtorName,
    string? DebtorCui,
    string? CourtName,
    string? CourtSection,
    string? JudgeSyndic,
    string? Registrar,
    string? ProcedureType,
    DateTime? OpeningDate,
    DateTime? NextHearingDate,
    DateTime? ClaimsDeadline,
    DateTime? ContestationsDeadline,
    List<AiExtractedPartyResult> Parties,
    double Confidence);

public sealed record AiExtractedPartyResult(
    string Role,
    string Name,
    string? FiscalId);
