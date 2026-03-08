using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public class NotificationService : INotificationService
{
  private readonly IApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;

  public NotificationService(IApplicationDbContext db, ICurrentUserService currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<Guid> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default)
  {
    var notification = new Notification
    {
      Id = Guid.NewGuid(),
      TenantId = _currentUser.TenantId ?? throw new InvalidOperationException("No tenant context"),
      UserId = dto.UserId,
      Title = dto.Title,
      Message = dto.Message,
      Category = dto.Category,
      RelatedCaseId = dto.RelatedCaseId,
      RelatedEmailId = dto.RelatedEmailId,
      RelatedTaskId = dto.RelatedTaskId,
      ActionUrl = dto.ActionUrl,
      CreatedOn = DateTime.UtcNow,
    };

    _db.Notifications.Add(notification);
    await _db.SaveChangesAsync(ct);
    return notification.Id;
  }

  public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
  {
    var userId = _currentUser.UserId ?? throw new InvalidOperationException("No user context");
    return await _db.Notifications
      .Where(n => n.UserId == userId && !n.IsRead)
      .CountAsync(ct);
  }

  public async Task<List<NotificationDto>> GetRecentAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
  {
    var userId = _currentUser.UserId ?? throw new InvalidOperationException("No user context");
    return await _db.Notifications
      .Where(n => n.UserId == userId)
      .OrderByDescending(n => n.CreatedOn)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .Select(n => new NotificationDto(
        n.Id, n.Title, n.Message, n.Category,
        n.IsRead, n.CreatedOn, n.ReadAt,
        n.RelatedCaseId, n.RelatedEmailId, n.RelatedTaskId,
        n.ActionUrl))
      .ToListAsync(ct);
  }

  public async Task MarkReadAsync(Guid notificationId, CancellationToken ct = default)
  {
    var userId = _currentUser.UserId ?? throw new InvalidOperationException("No user context");
    var notification = await _db.Notifications
      .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
    if (notification is null) return;

    notification.IsRead = true;
    notification.ReadAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);
  }

  public async Task MarkAllReadAsync(CancellationToken ct = default)
  {
    var userId = _currentUser.UserId ?? throw new InvalidOperationException("No user context");
    var now = DateTime.UtcNow;
    var unread = await _db.Notifications
      .Where(n => n.UserId == userId && !n.IsRead)
      .ToListAsync(ct);
    foreach (var n in unread)
    {
      n.IsRead = true;
      n.ReadAt = now;
    }
    await _db.SaveChangesAsync(ct);
  }
}
