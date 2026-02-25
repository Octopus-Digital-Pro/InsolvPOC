using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/workflow")]
[Authorize]
[RequirePermission(Permission.StageView)]
public class StageTransitionController : ControllerBase
{
  private readonly WorkflowValidationService _workflow;
  private readonly ICurrentUserService _currentUser;
  private readonly IAuditService _audit;

  public StageTransitionController(WorkflowValidationService workflow, ICurrentUserService currentUser, IAuditService audit)
  {
    _workflow = workflow;
    _currentUser = currentUser;
    _audit = audit;
  }

  /// <summary>Get the stage timeline for a case (all stages with status).</summary>
  [HttpGet("{caseId:guid}/timeline")]
  public async Task<IActionResult> GetTimeline(Guid caseId)
  {
    var timeline = await _workflow.GetStageTimelineAsync(caseId);
    if (timeline.Count == 0) return NotFound("Case not found");
    return Ok(timeline);
  }

  /// <summary>Validate whether the case can advance from its current stage.</summary>
  [HttpGet("{caseId:guid}/validate")]
  public async Task<IActionResult> Validate(Guid caseId)
  {
    var result = await _workflow.ValidateStageAsync(caseId);
    if (result.Error != null) return BadRequest(new { message = result.Error });
    return Ok(result);
  }

  /// <summary>Advance the case to the next stage (if all validation gates pass).</summary>
  [HttpPost("{caseId:guid}/advance")]
  [RequirePermission(Permission.StageAdvance)]
  public async Task<IActionResult> Advance(Guid caseId)
  {
    if (!_currentUser.UserId.HasValue) return Unauthorized();

    var result = await _workflow.AdvanceStageAsync(caseId, _currentUser.UserId.Value);
    if (!result.Success)
    {
      await _audit.LogWorkflowAsync("Stage Advance Validation Failed", caseId,
         new { result.Error, result.FailedRules }, severity: "Warning");
      return BadRequest(new { message = result.Error, failedRules = result.FailedRules });
    }

    await _audit.LogWorkflowAsync("Case Advanced to Next Stage", caseId,
    new { previousStage = result.PreviousStage.ToString(), newStage = result.NewStage.ToString() },
       severity: "Critical");

    return Ok(new
    {
      message = $"Case advanced from {result.PreviousStage} to {result.NewStage}",
      previousStage = result.PreviousStage.ToString(),
      newStage = result.NewStage.ToString(),
    });
  }

  /// <summary>Get the workflow definition (all stage definitions).</summary>
  [HttpGet("stages")]
  public IActionResult GetStageDefinitions()
  {
    var stages = WorkflowDefinition.GetOrderedStages()
 .Select(s => new
 {
   stage = s.Stage.ToString(),
   s.Order,
   s.Name,
   s.Goal,
   nextStage = s.NextStage?.ToString(),
   s.RequiredTasks,
   s.RequiredDocTypes,
   validationRules = s.ValidationRules.Select(r => r.Description),
   s.AutoTaskCategories,
   templateTypes = s.TemplateTypes.Select(t => t.ToString()),
 });
    return Ok(stages);
  }
}
