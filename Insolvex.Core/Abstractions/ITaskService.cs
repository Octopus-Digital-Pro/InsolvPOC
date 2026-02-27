using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for task management (company-level and case-level).
/// Per InsolvencyAppRules: every action is a Task with mandatory deadline and assignee.
/// All operations are tenant-scoped and audited.
/// </summary>
public interface ITaskService
{
    Task<List<TaskDto>> GetAllAsync(Guid? companyId = null, bool? myTasks = null, CancellationToken ct = default);
    Task<TaskDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(CreateTaskCommand command, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid id, UpdateTaskCommand command, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Case-scoped operations
    Task<List<TaskDto>> GetByCaseAsync(Guid caseId,
        Domain.Enums.TaskStatus? status = null, string? category = null, CancellationToken ct = default);
    Task<CaseTaskSummaryResult> GetCaseTaskSummaryAsync(Guid caseId, CancellationToken ct = default);
    Task<TaskDto> CreateForCaseAsync(Guid caseId, CreateTaskCommand command, CancellationToken ct = default);

    // Notes / activity
    Task<List<TaskNoteDto>> GetNotesAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskNoteDto> AddNoteAsync(Guid taskId, string content, CancellationToken ct = default);
    Task<TaskNoteDto> UpdateNoteAsync(Guid noteId, string content, CancellationToken ct = default);
    Task DeleteNoteAsync(Guid noteId, CancellationToken ct = default);
}

public class CreateTaskCommand
{
    public Guid CompanyId { get; init; }
    public Guid? CaseId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Labels { get; init; }
    public string? Category { get; init; }
    public DateTime? Deadline { get; init; }
    public string? DeadlineSource { get; init; }
    public bool IsCriticalDeadline { get; init; }
    public Guid? AssignedToUserId { get; init; }
}

public class UpdateTaskCommand
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Labels { get; init; }
    public string? Category { get; init; }
    public DateTime? Deadline { get; init; }
    public Domain.Enums.TaskStatus? Status { get; init; }
    public string? BlockReason { get; init; }
    public Guid? AssignedToUserId { get; init; }
}

public class CaseTaskSummaryResult
{
    public int Total { get; init; }
    public int Open { get; init; }
    public int InProgress { get; init; }
    public int Blocked { get; init; }
    public int Done { get; init; }
    public int Overdue { get; init; }
    public int Critical { get; init; }
    public List<CategoryCount> ByCategory { get; init; } = new();
}

public record CategoryCount(string Category, int Count);
