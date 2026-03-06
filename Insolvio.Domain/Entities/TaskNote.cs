namespace Insolvio.Domain.Entities;

public class TaskNote : TenantScopedEntity
{
    public Guid TaskId { get; set; }
    public virtual CompanyTask? Task { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Display name of the user who created the note.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    public DateTime? UpdatedOn { get; set; }
}
