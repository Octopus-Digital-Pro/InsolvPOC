using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.Core.Abstractions;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
  private readonly INotificationService _notifications;

  public NotificationsController(INotificationService notifications)
  {
    _notifications = notifications;
  }

  [HttpGet]
  public async Task<IActionResult> GetRecent(
    [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    => Ok(await _notifications.GetRecentAsync(page, pageSize, ct));

  [HttpGet("unread-count")]
  public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    => Ok(new { count = await _notifications.GetUnreadCountAsync(ct) });

  [HttpPut("{id:guid}/read")]
  public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
  {
    await _notifications.MarkReadAsync(id, ct);
    return NoContent();
  }

  [HttpPut("read-all")]
  public async Task<IActionResult> MarkAllRead(CancellationToken ct)
  {
    await _notifications.MarkAllReadAsync(ct);
    return NoContent();
  }
}
