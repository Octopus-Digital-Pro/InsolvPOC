namespace Insolvio.Core.DTOs;

/// <summary>Read model for a court / tribunal record.</summary>
public record TribunalDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string? Section,
    string? Locality,
    string? County,
    string? Address,
    string? PostalCode,
    string? RegistryPhone,
    string? RegistryFax,
    string? RegistryEmail,
    string? RegistryHours,
    string? Website,
    string? ContactPerson,
    string? Notes,
    Guid? OverridesGlobalId,
    bool IsGlobal,
    bool IsTenantOverride
);

/// <summary>Write model for creating or updating a tribunal record.</summary>
public record TribunalRequest(
    string Name,
    string? Section,
    string? Locality,
    string? County,
    string? Address,
    string? PostalCode,
    string? RegistryPhone,
    string? RegistryFax,
    string? RegistryEmail,
    string? RegistryHours,
    string? Website,
    string? ContactPerson,
    string? Notes,
    Guid? OverridesGlobalId
);

/// <summary>CSV import/export row for tribunal records.</summary>
public class TribunalCsvRow
{
    public string? Name { get; set; }
    public string? Section { get; set; }
    public string? Locality { get; set; }
    public string? County { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? RegistryPhone { get; set; }
    public string? RegistryFax { get; set; }
    public string? RegistryEmail { get; set; }
    public string? RegistryHours { get; set; }
    public string? Website { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
}
