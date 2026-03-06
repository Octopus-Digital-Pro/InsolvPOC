namespace Insolvio.Domain.Entities;

/// <summary>
/// Local government (Prim?rie / Consiliu Local) contact information.
/// </summary>
public class LocalGovernment : BaseEntity
{
    public Guid? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Entity name, e.g. "Prim?ria Sector 1 Bucure?ti"</summary>
    public string Name { get; set; } = string.Empty;

    public string? Locality { get; set; }
    public string? County { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? ContactPerson { get; set; }
    public string? ScheduleHours { get; set; }
    public string? Notes { get; set; }
    public Guid? OverridesGlobalId { get; set; }
}
