using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
  private readonly ISettingsService _settings;
  private readonly IUserService _users;
  private readonly IErrorLogService _errors;

  public SettingsController(ISettingsService settings, IUserService users, IErrorLogService errors)
  {
    _settings = settings;
    _users = users;
    _errors = errors;
  }

  // ── Tenant Settings ────────────────────────────────────────────────────

  [HttpGet("tenant")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetTenantSettings(CancellationToken ct)
      => Ok(await _settings.GetTenantAsync(ct));

  [HttpPut("tenant")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> UpdateTenantSettings([FromBody] UpdateTenantSettingsRequest request, CancellationToken ct)
  {
    await _settings.UpdateTenantAsync(request, ct);
    return Ok(new { message = "Settings updated" });
  }

  // ── Scheduled Emails ───────────────────────────────────────────────────

  [HttpGet("emails")]
  [RequirePermission(Permission.EmailView)]
  public async Task<IActionResult> GetScheduledEmails(
      [FromQuery] bool? sent = null,
      [FromQuery] int page = 0,
      [FromQuery] int pageSize = 20,
      CancellationToken ct = default)
  {
    var (items, total) = await _settings.GetScheduledEmailsAsync(sent, page, pageSize, ct);
    return Ok(new { total, page, pageSize, items });
  }

  [HttpPost("emails")]
  [RequirePermission(Permission.EmailCreate)]
  public async Task<IActionResult> CreateScheduledEmail([FromBody] CreateScheduledEmailRequest request, CancellationToken ct)
      => Ok(await _settings.CreateScheduledEmailAsync(request, ct));

  [HttpDelete("emails/{id:guid}")]
  [RequirePermission(Permission.EmailDelete)]
  public async Task<IActionResult> DeleteScheduledEmail(Guid id, CancellationToken ct)
  {
    await _settings.DeleteScheduledEmailAsync(id, ct);
    return Ok(new { message = "Deleted" });
  }

  // ── System Config (global) ─────────────────────────────────────────────

  [HttpGet("config")]
  [RequirePermission(Permission.SystemConfigView)]
  public async Task<IActionResult> GetSystemConfig([FromQuery] string? group = null, CancellationToken ct = default)
      => Ok(await _settings.GetSystemConfigAsync(group, ct));

  [HttpPut("config")]
  [RequirePermission(Permission.SystemConfigEdit)]
  public async Task<IActionResult> UpdateSystemConfig([FromBody] UpdateSystemConfigRequest request, CancellationToken ct)
  {
    await _settings.UpdateSystemConfigAsync(request, ct);
    return Ok(new { message = "Configuration updated. Restart the application for storage provider changes to take effect." });
  }

  // ── Demo Reset ─────────────────────────────────────────────────────────

  [HttpPost("demo/reset")]
  [RequirePermission(Permission.DemoReset)]
  public async Task<IActionResult> DemoReset(CancellationToken ct)
      => Ok(await _settings.DemoResetAsync(ct));

  // ── Error Logs ─────────────────────────────────────────────────────────

  [HttpGet("errors")]
  [RequirePermission(Permission.ErrorLogView)]
  public async Task<IActionResult> GetErrorLogs(
      [FromQuery] bool? resolved = null,
      [FromQuery] int page = 0,
      [FromQuery] int pageSize = 20,
      CancellationToken ct = default)
  {
    var (items, total) = await _errors.GetAllAsync(page, pageSize, resolved, ct);
    return Ok(new { total, page, pageSize, items });
  }

  [HttpPut("errors/{id:guid}/resolve")]
  [RequirePermission(Permission.ErrorLogResolve)]
  public async Task<IActionResult> ResolveError(Guid id, CancellationToken ct)
  {
    await _errors.ResolveAsync(id, ct);
    return Ok(new { message = "Marked as resolved" });
  }

  [HttpPost("errors/client")]
  public async Task<IActionResult> LogClientError([FromBody] CreateClientErrorLogRequest request, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(request.Message))
      return BadRequest(new { message = "Message is required" });

    var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
    var userEmail = User?.FindFirstValue(ClaimTypes.Email);
    await _errors.CreateClientErrorAsync(request, userId, userEmail, ct);
    return Ok(new { message = "Client error logged" });
  }

  // ── Users (admin read) ─────────────────────────────────────────────────

  [HttpGet("users")]
  [RequirePermission(Permission.UserView)]
  public async Task<IActionResult> GetTenantUsers(CancellationToken ct)
      => Ok(await _users.GetAllAsync(ct));

  // ── Insolvency Firm ────────────────────────────────────────────────────

  [HttpGet("firm")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetFirm(CancellationToken ct)
      => Ok(await _settings.GetFirmAsync(ct));

  [HttpPut("firm")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> UpsertFirm([FromBody] UpsertInsolvencyFirmRequest request, CancellationToken ct)
      => Ok(await _settings.UpsertFirmAsync(request, ct));

  // ── Email Preferences ──────────────────────────────────────────────────

  [HttpGet("email-preferences")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetEmailPreferences(CancellationToken ct)
      => Ok(await _settings.GetSystemConfigAsync("Email", ct));

  [HttpPut("email-preferences")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> UpdateEmailPreferences([FromBody] UpdateSystemConfigRequest request, CancellationToken ct)
  {
    await _settings.UpdateSystemConfigAsync(request, ct);
    return Ok(new { message = "Email preferences updated" });
  }

  // ── Document Templates ─────────────────────────────────────────────────

  [HttpGet("templates")]
  [RequirePermission(Permission.TemplateView)]
  public async Task<IActionResult> GetTemplates(CancellationToken ct)
      => Ok(await _settings.GetTemplatesAsync(ct));

  [HttpPost("templates/upload")]
  [RequirePermission(Permission.TemplateManage)]
  [RequestSizeLimit(50_000_000)]
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> UploadTemplate(
      [FromForm] IFormFile file,
      [FromForm] string templateType,
      [FromForm] string? name = null,
      [FromForm] string? description = null,
      [FromForm] string? stage = null,
      [FromForm] bool global = false,
      CancellationToken ct = default)
      => Ok(await _settings.UploadTemplateAsync(file, templateType, name, description, stage, global, ct));

  [HttpDelete("templates/{id:guid}")]
  [RequirePermission(Permission.TemplateManage)]
  public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
  {
    await _settings.DeleteTemplateAsync(id, ct);
    return Ok(new { message = "Template deleted" });
  }

  [HttpGet("templates/{id:guid}/download")]
  [RequirePermission(Permission.TemplateView)]
  public async Task<IActionResult> DownloadTemplate(Guid id, CancellationToken ct)
  {
    var (stream, fileName, contentType) = await _settings.DownloadTemplateAsync(id, ct);
    return File(stream, contentType, fileName);
  }
}
