namespace Insolvex.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
}
