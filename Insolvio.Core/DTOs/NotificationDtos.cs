namespace Insolvio.Core.DTOs;

public record CreateNotificationDto(
  Guid UserId,
  string Title,
  string? Message,
  string Category,
  Guid? RelatedCaseId = null,
  Guid? RelatedEmailId = null,
  Guid? RelatedTaskId = null,
  string? ActionUrl = null);

public record NotificationDto(
  Guid Id,
  string Title,
  string? Message,
  string Category,
  bool IsRead,
  DateTime CreatedAt,
  DateTime? ReadAt,
  Guid? RelatedCaseId,
  Guid? RelatedEmailId,
  Guid? RelatedTaskId,
  string? ActionUrl);
