namespace Insolvio.Domain.Entities;

/// <summary>
/// In-app notification for a specific user (e.g. new inbound email, task deadline, system alert).
/// </summary>
public class Notification : TenantScopedEntity
{
  public Guid UserId { get; set; }
  public virtual User? User { get; set; }

  public required string Title { get; set; }
  public string? Message { get; set; }

  /// <summary>Email, Task, Deadline, System</summary>
  public required string Category { get; set; }

  public bool IsRead { get; set; }
  public DateTime? ReadAt { get; set; }

  // Navigation links
  public Guid? RelatedCaseId { get; set; }
  public Guid? RelatedEmailId { get; set; }
  public Guid? RelatedTaskId { get; set; }

  /// <summary>Deep link path within the SPA. E.g. "/cases/{id}?tab=emails"</summary>
  public string? ActionUrl { get; set; }
}
