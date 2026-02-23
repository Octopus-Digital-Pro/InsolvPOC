using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.API.Services;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/deadline-settings")]
[Authorize]
[RequirePermission(Permission.SettingsView)]
public class DeadlineSettingsController : ControllerBase
{
    private readonly DeadlineEngine _engine;

    public DeadlineSettingsController(DeadlineEngine engine) => _engine = engine;

    /// <summary>Get effective deadline settings (resolved from hierarchy).</summary>
    [HttpGet]
    public async Task<IActionResult> GetEffectiveSettings([FromQuery] Guid? caseId, [FromQuery] Guid? tenantId)
    {
        var settings = await _engine.GetEffectiveSettingsAsync(caseId, tenantId);
      return Ok(settings);
    }

    /// <summary>Preview computed deadlines from a given NoticeDate.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewDeadlines([FromQuery] DateTime noticeDate, [FromQuery] Guid? tenantId)
    {
        var deadlines = await _engine.ComputeBaselineDeadlinesAsync(noticeDate, tenantId);
        return Ok(deadlines);
    }

    /// <summary>Check if a date is a working day (Europe/Bucharest).</summary>
    [HttpGet("is-working-day")]
    public IActionResult IsWorkingDay([FromQuery] DateTime date)
  {
        return Ok(new { date, isWorkingDay = _engine.IsWorkingDay(date) });
    }
}
