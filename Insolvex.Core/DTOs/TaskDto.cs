using Insolvex.Domain.Enums;

namespace Insolvex.Core.DTOs;

public record TaskDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Title,
    string? Description,
    string? Labels,
    DateTime? Deadline,
    Domain.Enums.TaskStatus Status,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedOn
);

public record CreateTaskRequest(
    Guid CompanyId,
    string Title,
    string? Description,
    string? Labels,
    DateTime? Deadline,
    Guid? AssignedToUserId
);

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Labels,
    DateTime? Deadline,
    Domain.Enums.TaskStatus? Status,
    Guid? AssignedToUserId
);
