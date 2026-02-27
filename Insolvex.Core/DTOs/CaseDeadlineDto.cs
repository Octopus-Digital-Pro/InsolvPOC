namespace Insolvex.Core.DTOs;

public record CaseDeadlineDto(
    Guid Id,
    Guid CaseId,
    string Label,
    DateTime DueDate,
    bool IsCompleted,
    string? PhaseKey,
    /// <summary>manual | caseCreation | phaseStart</summary>
    string RelativeTo,
    int? OffsetDays,
    string? Notes
);

public record CreateCaseDeadlineBody(
    string Label,
    DateTime DueDate,
    string? PhaseKey,
    string RelativeTo,
    int? OffsetDays,
    string? Notes
);

public record UpdateCaseDeadlineBody(
    string? Label,
    DateTime? DueDate,
    bool? IsCompleted,
    string? PhaseKey,
    string? RelativeTo,
    int? OffsetDays,
    string? Notes
);
