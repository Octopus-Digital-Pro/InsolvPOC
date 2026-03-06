namespace Insolvio.Core.DTOs;

/// <summary>
/// Shared read model for Finance Authorities (ANAF) and Local Governments (Primărie).
/// Both entities have the same contact-info shape.
/// </summary>
public record AuthorityDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string? Locality,
    string? County,
    string? Address,
    string? PostalCode,
    string? Phone,
    string? Fax,
    string? Email,
    string? Website,
    string? ContactPerson,
    string? ScheduleHours,
    string? Notes,
    Guid? OverridesGlobalId,
    bool IsGlobal,
    bool IsTenantOverride,
    Guid? ParentId = null,
    string? ParentName = null
);

/// <summary>Write model for creating or updating a finance authority record.</summary>
public record FinanceAuthorityRequest(
    string Name,
    string? Locality,
    string? County,
    string? Address,
    string? PostalCode,
    string? Phone,
    string? Fax,
    string? Email,
    string? Website,
    string? ContactPerson,
    string? ScheduleHours,
    string? Notes,
    Guid? OverridesGlobalId,
    Guid? ParentId = null
);

/// <summary>Write model for creating or updating a local government record.</summary>
public record LocalGovernmentRequest(
    string Name,
    string? Locality,
    string? County,
    string? Address,
    string? PostalCode,
    string? Phone,
    string? Fax,
    string? Email,
    string? Website,
    string? ContactPerson,
    string? ScheduleHours,
    string? Notes,
    Guid? OverridesGlobalId
);

/// <summary>Shared CSV import/export row for FinanceAuthority and LocalGovernment (same columns).</summary>
public class AuthorityCsvRow
{
    public string? Name { get; set; }
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
}

/// <summary>Result returned after importing authority records from a CSV file.</summary>
public record AuthorityImportResult(int Imported, int ErrorCount, List<string> Errors);

/// <summary>Result returned after scraping ANAF offices from the public ANAF website.</summary>
public record AnafScrapeResult(int Created, int Updated, int ErrorCount, List<string> Errors);

/// <summary>Request body for the ANAF scrape endpoint.</summary>
public record AnafScrapeRequest(string Url);
