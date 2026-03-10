using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.TaskView)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks) => _tasks = tasks;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? companyId,
        [FromQuery] bool? myTasks,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 200,
        CancellationToken ct = default)
        => Ok(await _tasks.GetAllAsync(companyId, myTasks, page, Math.Min(pageSize, 500), ct));

    /// <summary>Returns all tasks where the authenticated user is primary or secondary assignee.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyTasks(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 200,
        CancellationToken ct = default)
        => Ok(await _tasks.GetMyTasksAsync(page, Math.Min(pageSize, 500), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _tasks.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequirePermission(Permission.TaskCreate)]
    public async Task<IActionResult> Create([FromBody] CreateTaskBody body, CancellationToken ct)
    {
        var dto = await _tasks.CreateAsync(new CreateTaskCommand
        {
            CompanyId = body.CompanyId,
            CaseId = body.CaseId,
            Title = body.Title,
            Description = body.Description,
            Labels = body.Labels,
            Category = body.Category,
            Deadline = body.Deadline,
            DeadlineSource = body.DeadlineSource,
            IsCriticalDeadline = body.IsCriticalDeadline,
            AssignedToUserId = body.AssignedToUserId,
        }, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Create an ad-hoc task independent of the workflow. Caller is always primary assignee.</summary>
    [HttpPost("adhoc")]
    [RequirePermission(Permission.TaskCreate)]
    public async Task<IActionResult> CreateAdHoc([FromBody] CreateAdHocTaskBody body, CancellationToken ct)
    {
        var dto = await _tasks.CreateAdHocAsync(new CreateAdHocTaskCommand
        {
            CompanyId = body.CompanyId,
            CaseId = body.CaseId,
            Title = body.Title,
            Description = body.Description,
            Deadline = body.Deadline,
            AdditionalAssigneeIds = body.AdditionalAssigneeIds ?? new List<Guid>(),
        }, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.TaskEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskBody body, CancellationToken ct)
    {
        var dto = await _tasks.UpdateAsync(id, new UpdateTaskCommand
        {
            Title = body.Title,
            Description = body.Description,
            Labels = body.Labels,
            Category = body.Category,
            Deadline = body.Deadline,
            Status = body.Status,
            BlockReason = body.BlockReason,
            AssignedToUserId = body.AssignedToUserId,
            ReportSummary = body.ReportSummary,
        }, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.TaskDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(id, ct);
        return NoContent();
    }

    // ── Assignees (Feature 1: Multiple Task Assignees) ──────────────────────

    /// <summary>List all assignees for a task (primary first, then secondary).</summary>
    [HttpGet("{taskId:guid}/assignees")]
    public async Task<IActionResult> GetAssignees(Guid taskId, CancellationToken ct)
        => Ok(await _tasks.GetAssigneesAsync(taskId, ct));

    /// <summary>Add a secondary assignee to a task. The target user must belong to the same tenant.</summary>
    [HttpPost("{taskId:guid}/assignees")]
    [RequirePermission(Permission.TaskEdit)]
    public async Task<IActionResult> AddAssignee(Guid taskId, [FromBody] AddAssigneeRequest body, CancellationToken ct)
    {
        var dto = await _tasks.AddAssigneeAsync(taskId, body.UserId, ct);
        return Ok(dto);
    }

    /// <summary>Remove a secondary assignee from a task. Cannot remove the primary assignee via this endpoint.</summary>
    [HttpDelete("{taskId:guid}/assignees/{userId:guid}")]
    [RequirePermission(Permission.TaskEdit)]
    public async Task<IActionResult> RemoveAssignee(Guid taskId, Guid userId, CancellationToken ct)
    {
        await _tasks.RemoveAssigneeAsync(taskId, userId, ct);
        return NoContent();
    }
}

public record CreateTaskBody(
    Guid CompanyId, string Title, Guid? CaseId = null, string? Description = null,
    string? Labels = null, string? Category = null, DateTime? Deadline = null,
    string? DeadlineSource = null, bool IsCriticalDeadline = false, Guid? AssignedToUserId = null);

public record CreateAdHocTaskBody(
    Guid CompanyId, string Title, Guid? CaseId = null, string? Description = null,
    DateTime? Deadline = null, List<Guid>? AdditionalAssigneeIds = null);

public record UpdateTaskBody(
    string? Title = null, string? Description = null, string? Labels = null,
    string? Category = null, DateTime? Deadline = null, Domain.Enums.TaskStatus? Status = null,
    string? BlockReason = null, Guid? AssignedToUserId = null,
    string? ReportSummary = null);
