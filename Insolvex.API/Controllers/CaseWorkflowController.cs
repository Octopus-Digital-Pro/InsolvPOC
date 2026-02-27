using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.Core.Abstractions;
using Insolvex.API.Authorization;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// Per-case workflow stages: progression, validation, gating.
/// </summary>
[ApiController]
[Route("api/cases/{caseId:guid}/workflow")]
[Authorize]
public class CaseWorkflowController : ControllerBase
{
    private readonly ICaseWorkflowService _workflow;

    public CaseWorkflowController(ICaseWorkflowService workflow) => _workflow = workflow;

    /// <summary>Get all workflow stages for a case (auto-initializes if needed).</summary>
    [HttpGet]
    public async Task<IActionResult> GetStages(Guid caseId, CancellationToken ct)
        => Ok(await _workflow.GetStagesAsync(caseId, ct));

    /// <summary>Validate a specific stage's requirements.</summary>
    [HttpGet("{stageKey}/validate")]
    public async Task<IActionResult> ValidateStage(Guid caseId, string stageKey, CancellationToken ct)
        => Ok(await _workflow.ValidateStageAsync(caseId, stageKey, ct));

    /// <summary>Start (advance to InProgress) a stage. Gates on prior stages being done.</summary>
    [HttpPost("{stageKey}/start")]
    public async Task<IActionResult> StartStage(Guid caseId, string stageKey, CancellationToken ct)
        => Ok(await _workflow.StartStageAsync(caseId, stageKey, ct));

    /// <summary>Complete a stage. Validates requirements before allowing.</summary>
    [HttpPost("{stageKey}/complete")]
    public async Task<IActionResult> CompleteStage(Guid caseId, string stageKey, CancellationToken ct)
        => Ok(await _workflow.CompleteStageAsync(caseId, stageKey, ct));

    /// <summary>Skip a stage (not applicable for this case).</summary>
    [HttpPost("{stageKey}/skip")]
    public async Task<IActionResult> SkipStage(Guid caseId, string stageKey, [FromBody] SkipStageBody? body, CancellationToken ct)
        => Ok(await _workflow.SkipStageAsync(caseId, stageKey, body?.Reason, ct));

    /// <summary>Reopen a completed or skipped stage.</summary>
    [HttpPost("{stageKey}/reopen")]
    public async Task<IActionResult> ReopenStage(Guid caseId, string stageKey, CancellationToken ct)
        => Ok(await _workflow.ReopenStageAsync(caseId, stageKey, ct));

    // ── Case close ──────────────────────────────────────────────────────

    /// <summary>Returns whether the case can be closed and which stages are still pending.</summary>
    [HttpGet("closeability")]
    public async Task<IActionResult> GetCloseability(Guid caseId, CancellationToken ct)
        => Ok(await _workflow.GetCloseabilityAsync(caseId, ct));

    /// <summary>Close the case. Requires all stages completed/skipped or an override with explanation.</summary>
    [HttpPost("close")]
    [RequirePermission(Permission.CaseClose)]
    public async Task<IActionResult> CloseCase(Guid caseId, [FromBody] CloseCaseBody body, CancellationToken ct)
    {
        await _workflow.CloseCaseAsync(caseId, body.Explanation, body.OverridePendingStages, ct);
        return Ok(new { message = "Case closed successfully." });
    }

    // ── Stage deadline override ─────────────────────────────────────────

    /// <summary>Override the deadline for a specific workflow stage. Tenant admin only.</summary>
    [HttpPut("{stageKey}/deadline")]
    [RequirePermission(Permission.PhaseDeadlineOverride)]
    public async Task<IActionResult> SetStageDeadline(
        Guid caseId, string stageKey, [FromBody] SetStageDeadlineBody body, CancellationToken ct)
        => Ok(await _workflow.SetStageDeadlineAsync(caseId, stageKey, body.NewDate, body.Note, ct));
}

public record SkipStageBody(string? Reason = null);
public record CloseCaseBody(string? Explanation = null, bool OverridePendingStages = false);
public record SetStageDeadlineBody(DateTime NewDate, string Note);
