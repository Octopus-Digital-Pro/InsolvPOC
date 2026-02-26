using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Data.Services;

public sealed class TaskService : ITaskService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly SummaryRefreshService _summaryRefresh;
    private readonly ICaseEventService _caseEvents;

    public TaskService(ApplicationDbContext db, ICurrentUserService currentUser,
            IAuditService audit, SummaryRefreshService summaryRefresh, ICaseEventService caseEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _summaryRefresh = summaryRefresh;
        _caseEvents = caseEvents;
    }

    public async Task<List<TaskDto>> GetAllAsync(Guid? companyId, bool? myTasks, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.CompanyTasks
    .Include(t => t.Company).Include(t => t.AssignedTo).Include(t => t.Case)
     .Where(t => tenantId == null || t.TenantId == tenantId);

        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId);
        if (myTasks == true && _currentUser.UserId.HasValue)
            query = query.Where(t => t.AssignedToUserId == _currentUser.UserId);

        return await query.OrderBy(t => t.Deadline).Select(t => t.ToDto()).ToListAsync(ct);
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
        }
        if (cmd.AssignedToUserId.HasValue) task.AssignedToUserId = cmd.AssignedToUserId;

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
}
