namespace Insolvex.Domain.Entities;

/// <summary>
/// Global system configuration stored in the database.
/// Singleton-style record (one row per key). Used for settings like
/// storage provider selection, deadline defaults, etc.
/// </summary>
public class SystemConfig : BaseEntity
{
 /// <summary>Unique config key (e.g. "StorageProvider", "DefaultClaimDeadlineDays").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Config value stored as string (parse as needed).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional description for the setting.</summary>
    public string? Description { get; set; }

    /// <summary>Group for UI display (e.g. "Storage", "Deadlines", "Email").</summary>
    public string? Group { get; set; }
}
