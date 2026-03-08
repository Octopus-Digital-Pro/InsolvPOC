using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

public interface INotificationService
{
  Task<Guid> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default);
  Task<int> GetUnreadCountAsync(CancellationToken ct = default);
  Task<List<NotificationDto>> GetRecentAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
  Task MarkReadAsync(Guid notificationId, CancellationToken ct = default);
  Task MarkAllReadAsync(CancellationToken ct = default);
}
