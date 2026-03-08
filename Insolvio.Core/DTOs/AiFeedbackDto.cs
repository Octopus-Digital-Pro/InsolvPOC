namespace Insolvio.Core.DTOs;

public record AiCorrectionFeedbackDto(
    string DocumentType,
    string FieldName,
    string AiSuggestedValue,
    string UserCorrectedValue,
    bool WasAccepted,
    float? AiConfidence,
    string? DocumentTextSnippet,
    string Source);

public record AiFeedbackStatsDto(
    int TotalCorrections,
    int AcceptedCount,
    int CorrectedCount,
    double AcceptanceRate,
    Dictionary<string, FieldAccuracyDto> FieldAccuracy);

public record FieldAccuracyDto(
    string FieldName,
    int Total,
    int Accepted,
    double AcceptanceRate);
