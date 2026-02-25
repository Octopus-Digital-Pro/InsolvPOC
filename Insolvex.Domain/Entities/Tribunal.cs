namespace Insolvex.Domain.Entities;

/// <summary>
/// Court/Tribunal contact information.
/// GlobalAdmins upload master CSV. TenantAdmins can override specific entries for their tenant.
/// </summary>
public class Tribunal : BaseEntity
{
    /// <summary>Null = global record (uploaded by GlobalAdmin). Non-null = tenant override.</summary>
    public Guid? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Court name, e.g. "Tribunalul Bucure?ti"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Section, e.g. "Sec?ia a II-a civil?"</summary>
    public string? Section { get; set; }

    /// <summary>City/locality</summary>
    public string? Locality { get; set; }

    /// <summary>County, e.g. "Bucure?ti"</summary>
    public string? County { get; set; }

    /// <summary>Full address</summary>
    public string? Address { get; set; }

    /// <summary>Postal code</summary>
    public string? PostalCode { get; set; }

    /// <summary>Registry phone number</summary>
    public string? RegistryPhone { get; set; }

    /// <summary>Registry fax</summary>
    public string? RegistryFax { get; set; }

    /// <summary>Registry email</summary>
    public string? RegistryEmail { get; set; }

    /// <summary>Registry hours, e.g. "Luni-Vineri: 08:30-16:30"</summary>
    public string? RegistryHours { get; set; }

    /// <summary>Website URL</summary>
    public string? Website { get; set; }

    /// <summary>Contact person name</summary>
    public string? ContactPerson { get; set; }

    /// <summary>Additional notes</summary>
    public string? Notes { get; set; }

    /// <summary>If this is a tenant override, the ID of the global record being overridden</summary>
    public Guid? OverridesGlobalId { get; set; }
}
