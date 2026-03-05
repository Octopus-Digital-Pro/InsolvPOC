namespace Insolvio.Core.DTOs;

public record CaseSummaryDto(
    Guid Id,
    string Text,
    string? TextByLanguageJson,
    string? NextActionsJson,
    string? RisksJson,
    string? UpcomingDeadlinesJson,
    DateTime GeneratedAt,
    string? Trigger,
    string? Model
);

public record CaseSummaryHistoryItem(
    Guid Id,
    DateTime GeneratedAt,
    string? Trigger,
    string? Model
);
