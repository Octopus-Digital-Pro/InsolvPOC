using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/deadline-settings")]
[Authorize]
[RequirePermission(Permission.SettingsView)]
public class DeadlineSettingsController : ControllerBase
{
    private readonly DeadlineEngine _engine;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DeadlineSettingsController(DeadlineEngine engine, ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _engine = engine;
        _db = db;
_currentUser = currentUser;
        _audit = audit;
    }

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

 // ?? Tenant Deadline Settings CRUD ???????????????????????

    /// <summary>Get tenant deadline settings for the current tenant.</summary>
    [HttpGet("tenant")]
    public async Task<IActionResult> GetTenantSettings()
    {
      if (!_currentUser.TenantId.HasValue) return Unauthorized();

        var settings = await _db.TenantDeadlineSettings
 .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId.Value);

 if (settings == null)
            return Ok(new DeadlineSettings()); // Return defaults

        return Ok(settings);
    }

 /// <summary>Create or update tenant deadline settings.</summary>
    [HttpPut("tenant")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpsertTenantSettings([FromBody] UpdateTenantDeadlineSettingsRequest request)
    {
        if (!_currentUser.TenantId.HasValue) return Unauthorized();

        var existing = await _db.TenantDeadlineSettings
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId.Value);

 if (existing == null)
     {
            existing = new TenantDeadlineSettings { TenantId = _currentUser.TenantId.Value };
   _db.TenantDeadlineSettings.Add(existing);
        }

      if (request.SendInitialNoticeWithinDays.HasValue) existing.SendInitialNoticeWithinDays = request.SendInitialNoticeWithinDays.Value;
  if (request.ClaimDeadlineDaysFromNotice.HasValue) existing.ClaimDeadlineDaysFromNotice = request.ClaimDeadlineDaysFromNotice.Value;
        if (request.ObjectionDeadlineDaysFromNotice.HasValue) existing.ObjectionDeadlineDaysFromNotice = request.ObjectionDeadlineDaysFromNotice.Value;
    if (request.MeetingNoticeMinimumDays.HasValue) existing.MeetingNoticeMinimumDays = request.MeetingNoticeMinimumDays.Value;
        if (request.ReportEveryNDays.HasValue) existing.ReportEveryNDays = request.ReportEveryNDays.Value;
        if (request.UseBusinessDays.HasValue) existing.UseBusinessDays = request.UseBusinessDays.Value;
  if (request.AdjustToNextWorkingDay.HasValue) existing.AdjustToNextWorkingDay = request.AdjustToNextWorkingDay.Value;
        if (request.ReminderDaysBeforeDeadline != null) existing.ReminderDaysBeforeDeadline = request.ReminderDaysBeforeDeadline;
        if (request.UrgentQueueHoursBeforeDeadline.HasValue) existing.UrgentQueueHoursBeforeDeadline = request.UrgentQueueHoursBeforeDeadline.Value;
   if (request.AutoAssignBackupOnCriticalOverdue.HasValue) existing.AutoAssignBackupOnCriticalOverdue = request.AutoAssignBackupOnCriticalOverdue.Value;
        if (request.EmailFromName != null) existing.EmailFromName = request.EmailFromName;

      await _db.SaveChangesAsync();
        await _audit.LogAsync("DeadlineSettings.Updated", existing.Id);

        return Ok(existing);
    }

    // ?? Case-Level Deadline Overrides ???????????????????????

    /// <summary>Get all active overrides for a case.</summary>
    [HttpGet("case/{caseId:guid}/overrides")]
    public async Task<IActionResult> GetCaseOverrides(Guid caseId)
    {
        var overrides = await _db.CaseDeadlineOverrides
     .Where(o => o.CaseId == caseId && o.IsActive)
            .OrderBy(o => o.DeadlineKey)
    .ToListAsync();

        return Ok(overrides);
    }

    /// <summary>Create a case-level deadline override (requires reason for audit).</summary>
    [HttpPost("case/{caseId:guid}/overrides")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> CreateCaseOverride(Guid caseId, [FromBody] CreateCaseDeadlineOverrideRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        return BadRequest(new { message = "Reason is mandatory for deadline overrides." });

        var caseEntity = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId);
      if (caseEntity == null) return NotFound("Case not found");

        // Deactivate any existing override for the same key
        var existing = await _db.CaseDeadlineOverrides
            .Where(o => o.CaseId == caseId && o.DeadlineKey == request.DeadlineKey && o.IsActive)
    .ToListAsync();

        foreach (var old in existing) old.IsActive = false;

        // Get original value from effective settings (before this override)
        var currentSettings = await _engine.GetEffectiveSettingsAsync(caseId, null);
      var originalValue = request.DeadlineKey switch
  {
        "ClaimDeadlineDaysFromNotice" => currentSettings.ClaimDeadlineDaysFromNotice.ToString(),
            "ObjectionDeadlineDaysFromNotice" => currentSettings.ObjectionDeadlineDaysFromNotice.ToString(),
    "SendInitialNoticeWithinDays" => currentSettings.SendInitialNoticeWithinDays.ToString(),
         "MeetingNoticeMinimumDays" => currentSettings.MeetingNoticeMinimumDays.ToString(),
            "ReportEveryNDays" => currentSettings.ReportEveryNDays.ToString(),
     _ => null,
        };

     var newOverride = new CaseDeadlineOverride
        {
    TenantId = caseEntity.TenantId,
            CaseId = caseId,
 DeadlineKey = request.DeadlineKey,
        OriginalValue = originalValue,
            OverrideValue = request.OverrideValue,
            Reason = request.Reason,
 OverriddenByUserId = _currentUser.UserId,
            OverriddenAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.CaseDeadlineOverrides.Add(newOverride);
        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("DeadlineOverride.Created", "CaseDeadlineOverride", newOverride.Id,
            newValues: new { request.DeadlineKey, originalValue, request.OverrideValue, request.Reason },
            severity: "Warning");

        return Ok(newOverride);
    }

    /// <summary>Deactivate a case override.</summary>
    [HttpDelete("case/{caseId:guid}/overrides/{overrideId:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> DeactivateCaseOverride(Guid caseId, Guid overrideId)
  {
        var overrideEntity = await _db.CaseDeadlineOverrides
      .FirstOrDefaultAsync(o => o.Id == overrideId && o.CaseId == caseId);

        if (overrideEntity == null) return NotFound();

        overrideEntity.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DeadlineOverride.Deactivated", overrideId);

        return Ok(new { message = "Override deactivated." });
    }
}

// ?? Request models ??????????????????????????????????????

public class UpdateTenantDeadlineSettingsRequest
{
    public int? SendInitialNoticeWithinDays { get; set; }
public int? ClaimDeadlineDaysFromNotice { get; set; }
    public int? ObjectionDeadlineDaysFromNotice { get; set; }
    public int? MeetingNoticeMinimumDays { get; set; }
    public int? ReportEveryNDays { get; set; }
public bool? UseBusinessDays { get; set; }
    public bool? AdjustToNextWorkingDay { get; set; }
    public string? ReminderDaysBeforeDeadline { get; set; }
    public int? UrgentQueueHoursBeforeDeadline { get; set; }
    public bool? AutoAssignBackupOnCriticalOverdue { get; set; }
    public string? EmailFromName { get; set; }
}

public class CreateCaseDeadlineOverrideRequest
{
    public string DeadlineKey { get; set; } = string.Empty;
    public string OverrideValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
