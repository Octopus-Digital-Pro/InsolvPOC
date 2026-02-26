using Insolvex.Domain.Entities;

namespace Insolvex.Core.DTOs;

/// <summary>
/// Per-case workflow stage status — returned in a list for the case pipeline view.
/// </summary>
public record CaseWorkflowStageDto(
    Guid Id,
    Guid CaseId,
    Guid StageDefinitionId,
    string StageKey,
    string Name,
    string? Description,
    int SortOrder,
    string Status,         // NotStarted, InProgress, Completed, Skipped
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? CompletedBy,
    ValidationResultDto? Validation
);

/// <summary>
/// Validation check result for a stage — shows what's missing.
/// </summary>
public record ValidationResultDto(
    bool CanComplete,
    List<string> MissingFields,
    List<string> MissingPartyRoles,
    List<string> MissingDocTypes,
    List<string> MissingTasks,
    List<string> Messages
);
