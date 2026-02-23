namespace Insolvex.Core.DTOs;

public record CasePhaseDto(
    Guid Id,
    Guid CaseId,
  string PhaseType,
    string Status,
    int SortOrder,
 DateTime? StartedOn,
    DateTime? CompletedOn,
    DateTime? DueDate,
    string? Notes,
    string? CourtDecisionRef,
    Guid? UpdatedByUserId
);

public record UpdateCasePhaseRequest(
    string? Status,
    DateTime? StartedOn,
    DateTime? CompletedOn,
    DateTime? DueDate,
    string? Notes,
    string? CourtDecisionRef
);
