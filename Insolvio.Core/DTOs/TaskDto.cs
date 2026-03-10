using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

/// <summary>Represents a single assignee on a task (primary or secondary).</summary>
public record AssigneeDto(
    Guid UserId,
    string FullName,
    string Email,
    string? AvatarUrl,
    DateTime AssignedAt,
    bool IsPrimary
);

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
    string? BlockReason,
    Guid? AssignedToUserId,
    string? AssignedToName,
    Guid? CreatedByUserId,
    DateTime? CompletedAt,
    DateTime CreatedOn,
    string? Summary,
    string? SummaryByLanguageJson,
    string? ReportSummary
)
{
    /// <summary>
    /// Full list of assignees (primary first, then secondary).
    /// Populated only when the caller explicitly includes assignees (GetByIdAsync, GetMyTasksAsync).
    /// </summary>
    public List<AssigneeDto> Assignees { get; init; } = new();

    /// <summary>True = created manually by a user; not tied to a workflow stage.</summary>
    public bool IsAdHoc { get; init; }

    /// <summary>Workflow stage this task belongs to (null for ad-hoc tasks).</summary>
    public Guid? WorkflowStageId { get; init; }
};

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

public record TaskNoteDto(
    Guid Id,
    Guid TaskId,
    string Content,
    string CreatedByName,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);

public record AddTaskNoteRequest(string Content);
public record UpdateTaskNoteRequest(string Content);

/// <summary>Request body for adding an assignee to a task.</summary>
public record AddAssigneeRequest(Guid UserId);

/// <summary>Command for creating an ad-hoc task (Feature 9).</summary>
public class CreateAdHocTaskCommand
{
    public Guid? CaseId { get; init; }
    public Guid CompanyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? Deadline { get; init; }
    /// <summary>Additional assignee IDs beyond the caller (who is always primary assignee).</summary>
    public List<Guid> AdditionalAssigneeIds { get; init; } = new();
}
