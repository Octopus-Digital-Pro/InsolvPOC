using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;
using TaskStatus = Insolvio.Domain.Enums.TaskStatus;

namespace Insolvio.Core.Services;

public sealed class TaskService : ITaskService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly SummaryRefreshService _summaryRefresh;
    private readonly ICaseEventService _caseEvents;

    public TaskService(IApplicationDbContext db, ICurrentUserService currentUser,
            IAuditService audit, SummaryRefreshService summaryRefresh, ICaseEventService caseEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _summaryRefresh = summaryRefresh;
        _caseEvents = caseEvents;
    }

    public async Task<List<TaskDto>> GetAllAsync(Guid? companyId, bool? myTasks, int page, int pageSize, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.CompanyTasks
    .AsNoTracking()
    .Include(t => t.Company).Include(t => t.AssignedTo).Include(t => t.Case)
     .Where(t => tenantId == null || t.TenantId == tenantId);

        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId);
        if (myTasks == true && _currentUser.UserId.HasValue)
            query = query.Where(t => t.AssignedToUserId == _currentUser.UserId);

        return await query
            .OrderBy(t => t.Deadline)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(t => t.ToDto())
            .ToListAsync(ct);
    }

    public async Task<TaskDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var task = await _db.CompanyTasks
 .Include(t => t.Company).Include(t => t.AssignedTo).Include(t => t.Case)
            .FirstOrDefaultAsync(t => t.Id == id && (tenantId == null || t.TenantId == tenantId), ct);
        return task?.ToDto();
    }

    public async Task<TaskDto> CreateAsync(CreateTaskCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context is required to create a task.");

        if (!await _db.Companies.AnyAsync(c => c.Id == cmd.CompanyId && c.TenantId == tenantId, ct))
            throw new BusinessException("Company not found within this tenant.");

        var task = BuildTask(tenantId, cmd);
        _db.CompanyTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Task Created",
            Description = $"Task '{task.Title}' was created with deadline {task.Deadline:dd MMM yyyy}.",
            EntityType = "CompanyTask",
            EntityId = task.Id,
            EntityName = task.Title,
            Severity = task.IsCriticalDeadline ? "Warning" : "Info",
            Category = "TaskManagement",
        });

        return task.ToDto();
    }

    public async Task<TaskDto> UpdateAsync(Guid id, UpdateTaskCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var task = await _db.CompanyTasks
       .FirstOrDefaultAsync(t => t.Id == id && (tenantId == null || t.TenantId == tenantId), ct)
       ?? throw new BusinessException($"Task {id} not found.");

        var oldStatus = task.Status;

        if (cmd.Title != null) task.Title = cmd.Title;
        if (cmd.Description != null) task.Description = cmd.Description;
        if (cmd.Labels != null) task.Labels = cmd.Labels;
        if (cmd.Category != null) task.Category = cmd.Category;
        if (cmd.Deadline.HasValue) task.Deadline = cmd.Deadline;
        if (cmd.Status.HasValue)
        {
            task.Status = cmd.Status.Value;
            if (cmd.Status.Value == TaskStatus.Done && !task.CompletedAt.HasValue)
                task.CompletedAt = DateTime.UtcNow;
            // Clear block reason if no longer blocked
            if (cmd.Status.Value != TaskStatus.Blocked)
                task.BlockReason = null;
        }
        if (cmd.BlockReason != null) task.BlockReason = cmd.BlockReason;
        if (cmd.AssignedToUserId.HasValue) task.AssignedToUserId = cmd.AssignedToUserId;
        if (cmd.ReportSummary is not null) task.ReportSummary = cmd.ReportSummary == string.Empty ? null : cmd.ReportSummary;

        var summaries = LocalizedSummaryBuilder.BuildTaskSummaryByLanguage(
            task.Title,
            task.Description,
            task.Category,
            task.Deadline,
            task.Status);
        task.Summary = summaries["en"];
        task.SummaryByLanguageJson = JsonSerializer.Serialize(summaries);

        task.LastModifiedOn = DateTime.UtcNow;
        task.LastModifiedBy = _currentUser.Email;

        await _db.SaveChangesAsync(ct);

        var description = cmd.Status.HasValue && oldStatus != cmd.Status.Value
            ? $"Task '{task.Title}' status changed from '{oldStatus}' to '{cmd.Status.Value}'."
            : $"Task '{task.Title}' was updated.";

        await _audit.LogAsync(new AuditEntry
        {
            Action = cmd.Status.HasValue ? "Task Status Changed" : "Task Updated",
            Description = description,
            EntityType = "CompanyTask",
            EntityId = task.Id,
            EntityName = task.Title,
            Severity = "Info",
            Category = "TaskManagement",
        });

        // Auto-refresh case summary on task status change
        if (cmd.Status.HasValue && task.CaseId.HasValue)
        {
            _ = _summaryRefresh.RefreshIfStaleAsync(task.CaseId.Value, task.TenantId, "task_status_change");

            var evtType = cmd.Status.Value == TaskStatus.Done ? "Task.Completed"
                        : cmd.Status.Value == TaskStatus.Blocked ? "Task.Blocked"
                        : "Task.Updated";
            _ = _caseEvents.RecordTaskEventAsync(
                caseId: task.CaseId.Value,
                taskId: task.Id,
                eventType: evtType,
                taskTitle: task.Title,
                description: description,
                involvedParties: null,
                deadline: task.Deadline,
                ct: default);
        }

        return task.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var task = await _db.CompanyTasks
              .FirstOrDefaultAsync(t => t.Id == id && (tenantId == null || t.TenantId == tenantId), ct)
              ?? throw new BusinessException($"Task {id} not found.");

        _db.CompanyTasks.Remove(task);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Task Deleted",
            Description = $"Task '{task.Title}' was permanently deleted.",
            EntityType = "CompanyTask",
            EntityId = id,
            EntityName = task.Title,
            Severity = "Warning",
            Category = "TaskManagement",
        });
    }

    // ?? Case-scoped ?????????????????????????????????????????

    public async Task<List<TaskDto>> GetByCaseAsync(Guid caseId,
      Domain.Enums.TaskStatus? status, string? category, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.CompanyTasks
         .Include(t => t.Company).Include(t => t.AssignedTo).Include(t => t.Case)
            .Where(t => t.CaseId == caseId && (tenantId == null || t.TenantId == tenantId));

        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(t => t.Category == category);

        return await query.OrderBy(t => t.Deadline).Select(t => t.ToDto()).ToListAsync(ct);
    }

    public async Task<CaseTaskSummaryResult> GetCaseTaskSummaryAsync(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var tasks = await _db.CompanyTasks
  .Where(t => t.CaseId == caseId && (tenantId == null || t.TenantId == tenantId))
     .ToListAsync(ct);
        var now = DateTime.UtcNow;

        return new CaseTaskSummaryResult
        {
            Total = tasks.Count,
            Open = tasks.Count(t => t.Status == TaskStatus.Open),
            InProgress = tasks.Count(t => t.Status == TaskStatus.InProgress),
            Blocked = tasks.Count(t => t.Status == TaskStatus.Blocked),
            Done = tasks.Count(t => t.Status == TaskStatus.Done),
            Overdue = tasks.Count(t => t.Status == TaskStatus.Overdue
              || (t.Deadline < now && t.Status != TaskStatus.Done && t.Status != TaskStatus.Cancelled)),
            Critical = tasks.Count(t => t.IsCriticalDeadline && t.Status != TaskStatus.Done && t.Status != TaskStatus.Cancelled),
            ByCategory = tasks.GroupBy(t => t.Category ?? "Other")
    .Select(g => new CategoryCount(g.Key, g.Count())).ToList(),
        };
    }

    public async Task<TaskDto> CreateForCaseAsync(Guid caseId, CreateTaskCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
        ?? throw new BusinessException("Tenant context is required.");

        var caseEntity = await _db.InsolvencyCases
           .FirstOrDefaultAsync(c => c.Id == caseId && c.TenantId == tenantId, ct)
          ?? throw new BusinessException("Case not found.");

        if (!cmd.Deadline.HasValue)
            throw new BusinessException("Deadline is mandatory for all tasks per InsolvencyAppRules.");

        var companyId = cmd.CompanyId != Guid.Empty ? cmd.CompanyId
                  : caseEntity.CompanyId ?? throw new BusinessException("CompanyId is required (case has no linked company).");

        var task = BuildTask(tenantId, new CreateTaskCommand
        {
            CompanyId = companyId,
            CaseId = caseId,
            Title = cmd.Title,
            Description = cmd.Description,
            Labels = cmd.Labels,
            Category = cmd.Category,
            Deadline = cmd.Deadline,
            DeadlineSource = cmd.DeadlineSource,
            IsCriticalDeadline = cmd.IsCriticalDeadline,
            AssignedToUserId = cmd.AssignedToUserId,
        });
        _db.CompanyTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Task Created for Case",
            Description = $"Task '{task.Title}' was created for case '{caseEntity.CaseNumber}' with deadline {task.Deadline:dd MMM yyyy}.",
            EntityType = "CompanyTask",
            EntityId = task.Id,
            EntityName = task.Title,
            CaseNumber = caseEntity.CaseNumber,
            Severity = task.IsCriticalDeadline ? "Warning" : "Info",
            Category = "TaskManagement",
        });

        _ = _caseEvents.RecordTaskEventAsync(
            caseId: caseId,
            taskId: task.Id,
            eventType: "Task.Created",
            taskTitle: task.Title,
            description: $"Task '{task.Title}' created with deadline {task.Deadline:dd MMM yyyy}.",
            involvedParties: null,
            deadline: task.Deadline,
            ct: default);

        return task.ToDto();
    }

    // ?? Helpers ??????????????????????????????????????????????

    private CompanyTask BuildTask(Guid tenantId, CreateTaskCommand cmd)
    {
        var summaries = LocalizedSummaryBuilder.BuildTaskSummaryByLanguage(
            cmd.Title,
            cmd.Description,
            cmd.Category,
            cmd.Deadline,
            TaskStatus.Open);

        return new CompanyTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = cmd.CompanyId,
            CaseId = cmd.CaseId,
            Title = cmd.Title,
            Description = cmd.Description,
            Labels = cmd.Labels,
            Category = cmd.Category,
            Deadline = cmd.Deadline,
            DeadlineSource = cmd.DeadlineSource ?? "Manual",
            IsCriticalDeadline = cmd.IsCriticalDeadline,
            Status = TaskStatus.Open,
            Summary = summaries["en"],
            SummaryByLanguageJson = JsonSerializer.Serialize(summaries),
            AssignedToUserId = cmd.AssignedToUserId ?? _currentUser.UserId,
            CreatedByUserId = _currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };
    }

    // ?? Notes ????????????????????????????????????????????????

    public async Task<List<TaskNoteDto>> GetNotesAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        // Verify the task is accessible
        var taskExists = await _db.CompanyTasks
            .AnyAsync(t => t.Id == taskId && (tenantId == null || t.TenantId == tenantId), ct);
        if (!taskExists) return new List<TaskNoteDto>();

        return await _db.TaskNotes
            .Where(n => n.TaskId == taskId)
            .OrderBy(n => n.CreatedOn)
            .Select(n => new TaskNoteDto(n.Id, n.TaskId, n.Content, n.CreatedByName, n.CreatedOn, n.UpdatedOn))
            .ToListAsync(ct);
    }

    public async Task<TaskNoteDto> AddNoteAsync(Guid taskId, string content, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        var task = await _db.CompanyTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct)
            ?? throw new BusinessException("Task not found.");

        var note = new TaskNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = taskId,
            Content = content,
            CreatedByName = _currentUser.Email ?? "Unknown",
            CreatedOn = DateTime.UtcNow,
        };

        _db.TaskNotes.Add(note);
        await _db.SaveChangesAsync(ct);

        return new TaskNoteDto(note.Id, note.TaskId, note.Content, note.CreatedByName, note.CreatedOn, note.UpdatedOn);
    }

    public async Task<TaskNoteDto> UpdateNoteAsync(Guid noteId, string content, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var note = await _db.TaskNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && (tenantId == null || n.TenantId == tenantId), ct)
            ?? throw new BusinessException("Note not found.");

        note.Content = content;
        note.UpdatedOn = DateTime.UtcNow;
        note.LastModifiedOn = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new TaskNoteDto(note.Id, note.TaskId, note.Content, note.CreatedByName, note.CreatedOn, note.UpdatedOn);
    }

    public async Task DeleteNoteAsync(Guid noteId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var note = await _db.TaskNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && (tenantId == null || n.TenantId == tenantId), ct)
            ?? throw new BusinessException("Note not found.");

        _db.TaskNotes.Remove(note);
        await _db.SaveChangesAsync(ct);
    }

    // ── Feature 1: Multiple Task Assignees ──────────────────────────────────

    public async Task<List<AssigneeDto>> GetAssigneesAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;

        // Ensure the task is accessible
        var task = await _db.CompanyTasks
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == taskId && (tenantId == null || t.TenantId == tenantId), ct)
            ?? throw new BusinessException("Task not found.");

        // Build the primary assignee entry
        var result = new List<AssigneeDto>();
        if (task.AssignedToUserId.HasValue && task.AssignedTo is not null)
        {
            result.Add(new AssigneeDto(
                task.AssignedTo.Id,
                task.AssignedTo.FullName,
                task.AssignedTo.Email,
                task.AssignedTo.AvatarUrl,
                task.CreatedOn,
                IsPrimary: true));
        }

        // Secondary assignees
        var secondary = await _db.TaskAssignees
            .Include(a => a.User)
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(ct);

        foreach (var a in secondary)
        {
            if (a.User is null) continue;
            // Skip if the secondary assignee is the same as the primary
            if (a.UserId == task.AssignedToUserId) continue;
            result.Add(new AssigneeDto(
                a.User.Id,
                a.User.FullName,
                a.User.Email,
                a.User.AvatarUrl,
                a.AssignedAt,
                IsPrimary: false));
        }

        return result;
    }

    public async Task<AssigneeDto> AddAssigneeAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        var task = await _db.CompanyTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct)
            ?? throw new BusinessException("Task not found.");

        // Validate the user belongs to the same tenant
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new BusinessException("User not found in this tenant.");

        // Idempotent: if already a secondary assignee, return existing
        var existing = await _db.TaskAssignees
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId, ct);

        if (existing is not null)
        {
            return new AssigneeDto(user.Id, user.FullName, user.Email, user.AvatarUrl,
                existing.AssignedAt, IsPrimary: userId == task.AssignedToUserId);
        }

        var assignee = new TaskAssignee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = taskId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = _currentUser.UserId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email,
        };

        _db.TaskAssignees.Add(assignee);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = AuditActions.TaskAssigneeAdded,
            Description = $"User '{user.FullName}' was added as assignee to task '{task.Title}'.",
            EntityType = "CompanyTask",
            EntityId = taskId,
            EntityName = task.Title,
            Severity = "Info",
            Category = "TaskManagement",
        });

        return new AssigneeDto(user.Id, user.FullName, user.Email, user.AvatarUrl,
            assignee.AssignedAt, IsPrimary: false);
    }

    public async Task RemoveAssigneeAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        var task = await _db.CompanyTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct)
            ?? throw new BusinessException("Task not found.");

        // Cannot remove the primary assignee via this endpoint
        if (task.AssignedToUserId == userId)
            throw new BusinessException(
                "Cannot remove the primary assignee. Use UpdateAsync to change the primary assignee.");

        var assignee = await _db.TaskAssignees
            .FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId, ct)
            ?? throw new BusinessException("Assignee not found on this task.");

        _db.TaskAssignees.Remove(assignee);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        await _audit.LogAsync(new AuditEntry
        {
            Action = AuditActions.TaskAssigneeRemoved,
            Description = $"User '{user?.FullName ?? userId.ToString()}' was removed from task '{task.Title}'.",
            EntityType = "CompanyTask",
            EntityId = taskId,
            EntityName = task.Title,
            Severity = "Info",
            Category = "TaskManagement",
        });
    }

    // ── Feature 9: Ad-Hoc Task Creation ─────────────────────────────────────

    public async Task<TaskDto> CreateAdHocAsync(CreateAdHocTaskCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        if (!await _db.Companies.AnyAsync(c => c.Id == command.CompanyId && c.TenantId == tenantId, ct))
            throw new BusinessException("Company not found within this tenant.");

        var callerId = _currentUser.UserId
            ?? throw new BusinessException("User context required to create an ad-hoc task.");

        var summaries = LocalizedSummaryBuilder.BuildTaskSummaryByLanguage(
            command.Title, command.Description, "Ad-Hoc", command.Deadline, Domain.Enums.TaskStatus.Open);

        var task = new CompanyTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = command.CompanyId,
            CaseId = command.CaseId,
            Title = command.Title,
            Description = command.Description,
            Deadline = command.Deadline,
            DeadlineSource = "Manual",
            Category = "Ad-Hoc",
            Status = Domain.Enums.TaskStatus.Open,
            IsAdHoc = true,
            WorkflowStageId = null,
            AssignedToUserId = callerId,   // caller is always primary assignee
            CreatedByUserId = callerId,
            Summary = summaries["en"],
            SummaryByLanguageJson = JsonSerializer.Serialize(summaries),
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };

        _db.CompanyTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        // Add additional secondary assignees
        foreach (var assigneeId in command.AdditionalAssigneeIds.Distinct())
        {
            if (assigneeId == callerId) continue;   // skip if same as primary
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == assigneeId && u.TenantId == tenantId, ct);
            if (user is null) continue;

            _db.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TaskId = task.Id,
                UserId = assigneeId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = callerId,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = _currentUser.Email,
            });
        }

        if (command.AdditionalAssigneeIds.Any())
            await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = AuditActions.TaskCreated,
            Description = $"Ad-hoc task '{task.Title}' created by {_currentUser.Email}.",
            EntityType = "CompanyTask",
            EntityId = task.Id,
            EntityName = task.Title,
            Severity = "Info",
            Category = "TaskManagement",
        });

        return task.ToDto() with { IsAdHoc = true };
    }

    // ── "My Tasks" ───────────────────────────────────────────────────────────

    public async Task<List<TaskDto>> GetMyTasksAsync(int page = 0, int pageSize = 200, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return new List<TaskDto>();

        // Tasks where caller is primary assignee
        var primaryIds = await _db.CompanyTasks
            .Where(t => t.AssignedToUserId == userId && (tenantId == null || t.TenantId == tenantId))
            .Select(t => t.Id)
            .ToListAsync(ct);

        // Tasks where caller is a secondary assignee
        var secondaryIds = await _db.TaskAssignees
            .Where(a => a.UserId == userId && (tenantId == null || a.TenantId == tenantId))
            .Select(a => a.TaskId)
            .ToListAsync(ct);

        var allTaskIds = primaryIds.Union(secondaryIds).ToList();

        return await _db.CompanyTasks
            .Include(t => t.Company)
            .Include(t => t.AssignedTo)
            .Include(t => t.Case)
            .Where(t => allTaskIds.Contains(t.Id))
            .OrderBy(t => t.Deadline)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(t => t.ToDto())
            .ToListAsync(ct);
    }
}
