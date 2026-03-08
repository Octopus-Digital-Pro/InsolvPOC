using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

public interface IAiFeedbackService
{
    Task RecordCorrectionsAsync(IReadOnlyList<AiCorrectionFeedbackDto> corrections, CancellationToken ct = default);
    Task<AiFeedbackStatsDto> GetStatisticsAsync(string? documentType = null, CancellationToken ct = default);
}
