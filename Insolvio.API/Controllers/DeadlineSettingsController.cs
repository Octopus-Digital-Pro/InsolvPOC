using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Services;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/deadline-settings")]
[Authorize]
[RequirePermission(Permission.SettingsView)]
public class DeadlineSettingsController : ControllerBase
{
    private readonly DeadlineEngine _engine;
    private readonly IDeadlineSettingsService _deadlineSettings;

    public DeadlineSettingsController(DeadlineEngine engine, IDeadlineSettingsService deadlineSettings)
    {
        _engine = engine;
        _deadlineSettings = deadlineSettings;
    }

    /// <summary>Get effective deadline settings (resolved from hierarchy).</summary>
    [HttpGet]
    public async Task<IActionResult> GetEffectiveSettings([FromQuery] Guid? caseId, [FromQuery] Guid? tenantId)
        => Ok(await _engine.GetEffectiveSettingsAsync(caseId, tenantId));

    /// <summary>Preview computed deadlines from a given NoticeDate.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewDeadlines([FromQuery] DateTime noticeDate, [FromQuery] Guid? tenantId)
        => Ok(await _engine.ComputeBaselineDeadlinesAsync(noticeDate, tenantId));

    /// <summary>Check if a date is a working day (Europe/Bucharest).</summary>
    [HttpGet("is-working-day")]
    public IActionResult IsWorkingDay([FromQuery] DateTime date)
        => Ok(new { date, isWorkingDay = _engine.IsWorkingDay(date) });

    [HttpGet("tenant")]
    public async Task<IActionResult> GetTenantSettings(CancellationToken ct)
        => Ok(await _deadlineSettings.GetTenantSettingsAsync(ct));

    [HttpPut("tenant")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpsertTenantSettings([FromBody] UpdateTenantDeadlineSettingsRequest request, CancellationToken ct)
        => Ok(await _deadlineSettings.UpsertTenantSettingsAsync(request, ct));

    [HttpGet("case/{caseId:guid}/overrides")]
    public async Task<IActionResult> GetCaseOverrides(Guid caseId, CancellationToken ct)
        => Ok(await _deadlineSettings.GetCaseOverridesAsync(caseId, ct));

    [HttpPost("case/{caseId:guid}/overrides")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> CreateCaseOverride(Guid caseId, [FromBody] CreateCaseDeadlineOverrideRequest request, CancellationToken ct)
        => Ok(await _deadlineSettings.CreateCaseOverrideAsync(caseId, request, ct));

    [HttpDelete("case/{caseId:guid}/overrides/{overrideId:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> DeactivateCaseOverride(Guid caseId, Guid overrideId, CancellationToken ct)
    {
        await _deadlineSettings.DeactivateCaseOverrideAsync(caseId, overrideId, ct);
        return Ok(new { message = "Override deactivated." });
    }
}
