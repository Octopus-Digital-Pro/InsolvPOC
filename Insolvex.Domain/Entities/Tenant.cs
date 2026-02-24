using Insolvex.Domain.Enums;

namespace Insolvex.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? SubscriptionExpiry { get; set; }
    public string? PlanName { get; set; }

    /// <summary>System region determines which national firm registries are available.</summary>
    public SystemRegion Region { get; set; } = SystemRegion.Romania;

    /// <summary>Marks tenant as a demo tenant — enables the Demo Reset feature.</summary>
    public bool IsDemo { get; set; } = false;

    // Navigation
    public virtual InsolvencyFirm? InsolvencyFirm { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Company> Companies { get; set; } = new List<Company>();
    public ICollection<InsolvencyCase> Cases { get; set; } = new List<InsolvencyCase>();
}
