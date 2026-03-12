namespace Insolvio.Domain.Entities;

/// <summary>
/// Represents a country/region that the system supports.
/// Regions are global (not tenant-scoped) and can be managed by Global Admins.
/// </summary>
public class Region : BaseEntity
{
    /// <summary>Display name of the region (e.g. "Romania").</summary>
    public string Name { get; set; } = "";

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. "RO").</summary>
    public string IsoCode { get; set; } = "";

    /// <summary>Unicode flag emoji (e.g. "🇷🇴").</summary>
    public string Flag { get; set; } = "";

    /// <summary>Whether this is the default region. Only one row may be true at a time.</summary>
    public bool IsDefault { get; set; }
}
