using Insolvex.Domain.Enums;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Cross-system company lookup service.
/// Searches the tenant's own company database first, then falls through to the
/// regional ONRC firm database (ONRCFirmRecords) for authoritative data.
/// Per InsolvencyAppRules: firm search should always resolve against the
/// system region configured on the tenant.
/// </summary>
public interface IFirmLookupService
{
    /// <summary>
    /// Search for companies by CUI or name across all available sources
    /// (tenant companies + regional ONRC database).
    /// </summary>
    Task<FirmLookupResults> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default);

    /// <summary>Lookup a single firm by its CUI from the ONRC database.</summary>
    Task<FirmLookupItem?> GetByCuiAsync(string cui, CancellationToken ct = default);
}

/// <summary>Aggregated results from all lookup sources.</summary>
public class FirmLookupResults
{
    public List<FirmLookupItem> TenantCompanies { get; init; } = [];
    public List<FirmLookupItem> OnrcRecords { get; init; } = [];
    public SystemRegion Region { get; init; }

    public IEnumerable<FirmLookupItem> All => TenantCompanies.Concat(OnrcRecords);
}

/// <summary>Unified firm/company result from any lookup source.</summary>
public class FirmLookupItem
{
    public string? TenantCompanyId { get; init; }  // set if sourced from tenant Companies table
    public string CUI { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? TradeRegisterNo { get; init; }
    public string? Address { get; init; }
    public string? Locality { get; init; }
    public string? County { get; init; }
    public string? Phone { get; init; }
    public string? Status { get; init; }
    public string? CAEN { get; init; }
    public string Source { get; init; } = "ONRC"; // "TenantCompany" | "ONRC"
}
