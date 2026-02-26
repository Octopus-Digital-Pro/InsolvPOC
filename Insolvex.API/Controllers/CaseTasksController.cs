using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/tasks")]
[Authorize]
[RequirePermission(Permission.TaskView)]
public class CaseTasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public CaseTasksController(ITaskService tasks) => _tasks = tasks;

    [HttpGet]
    public async Task<IActionResult> GetCaseTasks(
        Guid caseId,
        [FromQuery] CaseStage? stage = null,
        [FromQuery] Domain.Enums.TaskStatus? status = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
        => Ok(await _tasks.GetByCaseAsync(caseId, stage, status, category, ct));

    [HttpGet("summary")]
    public async Task<IActionResult> GetTaskSummary(Guid caseId, CancellationToken ct)
        => Ok(await _tasks.GetCaseTaskSummaryAsync(caseId, ct));

    [HttpPost]
    [RequirePermission(Permission.TaskCreate)]
    public async Task<IActionResult> CreateCaseTask(Guid caseId, [FromBody] CreateTaskBody body, CancellationToken ct)
    {
        var dto = await _tasks.CreateForCaseAsync(caseId, new CreateTaskCommand
        {
            CompanyId = body.CompanyId,
            Title = body.Title,
            Description = body.Description,
            Labels = body.Labels,
            Category = body.Category,
            Deadline = body.Deadline,
            DeadlineSource = body.DeadlineSource,
            IsCriticalDeadline = body.IsCriticalDeadline,
            AssignedToUserId = body.AssignedToUserId,
        }, ct);
        return CreatedAtAction(nameof(GetCaseTasks), new { caseId }, dto);
    }
}
