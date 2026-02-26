using Insolvex.Domain.Enums;

namespace Insolvex.Core.DTOs;

public record TaskDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    Guid? CaseId,
    string? CaseNumber,
    string Title,
    string? Description,
    string? Labels,
    string? Category,
    DateTime? Deadline,
    string? DeadlineSource,
    bool IsCriticalDeadline,
    Domain.Enums.TaskStatus Status,
    Guid? AssignedToUserId,
    string? AssignedToName,
    Guid? CreatedByUserId,
    DateTime? CompletedAt,
    DateTime CreatedOn,
    string? Summary,
    string? SummaryByLanguageJson
);

public record CreateTaskRequest(
    Guid CompanyId,
    Guid? CaseId,
    string Title,
    string? Description,
    string? Labels,
    string? Category,
    DateTime? Deadline,
    string? DeadlineSource,
    bool IsCriticalDeadline,
    Guid? AssignedToUserId
);

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Labels,
    string? Category,
    DateTime? Deadline,
    Domain.Enums.TaskStatus? Status,
    Guid? AssignedToUserId
);
