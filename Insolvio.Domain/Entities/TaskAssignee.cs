namespace Insolvio.Domain.Entities;

/// <summary>
/// Represents a secondary assignee on a task (many-to-many between CompanyTask and User).
/// The primary assignee is still stored on CompanyTask.AssignedToUserId for backward compatibility.
/// </summary>
public class TaskAssignee : TenantScopedEntity
{
    public Guid TaskId { get; set; }
    public virtual CompanyTask? Task { get; set; }

    public Guid UserId { get; set; }
    public virtual User? User { get; set; }

    /// <summary>When this user was assigned to the task.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Who performed the assignment (userId of the actor).</summary>
    public Guid? AssignedByUserId { get; set; }
    public virtual User? AssignedBy { get; set; }
}
