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
    /// substring as it appears in the document. Returns <c>null</c> when AI is
    /// unavailable or the call fails.
    /// </summary>
    Task<Dictionary<string, string>?> SuggestAnnotationsAsync(
        string extractedText,
        CancellationToken ct = default);
}

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
