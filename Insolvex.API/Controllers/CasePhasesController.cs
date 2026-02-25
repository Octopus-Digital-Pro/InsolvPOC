using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/phases")]
[Authorize]
[RequirePermission(Permission.PhaseView)]
public class CasePhasesController : ControllerBase
{
    private readonly ICasePhasesService _phases;

    public CasePhasesController(ICasePhasesService phases) => _phases = phases;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _phases.GetAllAsync(caseId, ct));

    [HttpPost("initialize")]
    [RequirePermission(Permission.PhaseInitialize)]
    public async Task<IActionResult> Initialize(Guid caseId, CancellationToken ct)
        => Ok(await _phases.InitializeAsync(caseId, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.PhaseEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCasePhaseRequest req, CancellationToken ct)
    {
        var phase = await _phases.UpdateAsync(caseId, id, req, ct);
        if (phase is null) return NotFound();
        return Ok(phase);
    }

    [HttpPost("advance")]
    [RequirePermission(Permission.PhaseAdvance)]
    public async Task<IActionResult> Advance(Guid caseId, CancellationToken ct)
        => Ok(await _phases.AdvanceAsync(caseId, ct));

    [HttpGet("{id:guid}/requirements")]
    public async Task<IActionResult> GetRequirements(Guid caseId, Guid id, CancellationToken ct)
    {
        var result = await _phases.GetRequirementsAsync(caseId, id, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/generate-tasks")]
    [RequirePermission(Permission.PhaseEdit)]
    public async Task<IActionResult> GenerateTasks(Guid caseId, Guid id, CancellationToken ct)
    {
        var count = await _phases.GenerateTasksAsync(caseId, id, ct);
        return Ok(new { message = $"Generated {count} tasks", tasksGenerated = count });
    }
}
