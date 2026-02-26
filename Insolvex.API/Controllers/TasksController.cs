using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.TaskView)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks) => _tasks = tasks;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId, [FromQuery] bool? myTasks, CancellationToken ct)
        => Ok(await _tasks.GetAllAsync(companyId, myTasks, ct));

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
            CompanyId = body.CompanyId, CaseId = body.CaseId, Title = body.Title,
     Description = body.Description, Labels = body.Labels, Category = body.Category,
            Deadline = body.Deadline, DeadlineSource = body.DeadlineSource,
            IsCriticalDeadline = body.IsCriticalDeadline, AssignedToUserId = body.AssignedToUserId,
        }, ct);
   return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.TaskEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskBody body, CancellationToken ct)
    {
var dto = await _tasks.UpdateAsync(id, new UpdateTaskCommand
        {
     Title = body.Title, Description = body.Description, Labels = body.Labels,
            Category = body.Category, Deadline = body.Deadline, Status = body.Status,
            AssignedToUserId = body.AssignedToUserId,
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
}

public record CreateTaskBody(
    Guid CompanyId, string Title, Guid? CaseId = null, string? Description = null,
    string? Labels = null, string? Category = null, DateTime? Deadline = null,
    string? DeadlineSource = null, bool IsCriticalDeadline = false, Guid? AssignedToUserId = null);

public record UpdateTaskBody(
    string? Title = null, string? Description = null, string? Labels = null,
    string? Category = null, DateTime? Deadline = null, Domain.Enums.TaskStatus? Status = null,
    Guid? AssignedToUserId = null);
