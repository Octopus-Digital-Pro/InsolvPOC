using Insolvio.Domain.Entities;

namespace Insolvio.Core.DTOs;

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
    ValidationResultDto? Validation,
    DateTime? DeadlineDate,
    string? Notes,
    string? DeadlineOverrideNote,
    string? DeadlineOverriddenBy,
    DateTime? DeadlineOverriddenAt
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

/// <summary>Readiness info for closing a case.</summary>
public record CaseCloseabilityDto(
    bool CanClose,
    List<StageReadinessItem> PendingStages
);

/// <summary>A stage that is not yet complete or skipped.</summary>
public record StageReadinessItem(
    string StageKey,
    string Name,
    string Status
);
