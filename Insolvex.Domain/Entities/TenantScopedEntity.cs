namespace Insolvex.Domain.Entities;

public abstract class TenantScopedEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
