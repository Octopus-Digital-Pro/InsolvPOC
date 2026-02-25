using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Resolves firm lookups by searching:
/// 1. The tenant's own Companies table (CUI or Name match)
/// 2. The system-wide ONRCFirmRecords table, filtered to the tenant's region
/// </summary>
public sealed class FirmLookupService : IFirmLookupService
{
  private readonly ApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;

  public FirmLookupService(ApplicationDbContext db, ICurrentUserService currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<FirmLookupResults> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default)
  {
    var region = await GetTenantRegionAsync(ct);
    var trimmed = query.Trim();

    // 1. Search tenant companies (query filter auto-applies TenantId)
    var numericPart = trimmed.TrimStart('R', 'O', 'r', 'o');
    var isCui = numericPart.Length > 0 && numericPart.All(char.IsDigit);

    var tenantQuery = _db.Companies.AsQueryable();
    if (isCui)
      tenantQuery = tenantQuery.Where(c => c.CuiRo != null && c.CuiRo.StartsWith(numericPart));
    else
      tenantQuery = tenantQuery.Where(c => c.Name.Contains(trimmed));

    var tenantResults = await tenantQuery
        .OrderBy(c => c.Name)
  .Take(maxResults)
     .Select(c => new FirmLookupItem
     {
       TenantCompanyId = c.Id.ToString(),
       CUI = c.CuiRo ?? string.Empty,
       Name = c.Name,
       TradeRegisterNo = c.TradeRegisterNo,
       Address = c.Address,
       Locality = c.Locality,
       County = c.County,
       Phone = c.Phone,
       CAEN = c.Caen,
       Source = "TenantCompany",
     })
  .ToListAsync(ct);

    // 2. Search ONRC (region-filtered, no tenant query filter)
    var onrcQuery = _db.ONRCFirmRecords
    .IgnoreQueryFilters()
            .Where(f => f.Region == region);

    if (isCui)
      onrcQuery = onrcQuery.Where(f => f.CUI.StartsWith(numericPart));
    else
      onrcQuery = onrcQuery.Where(f => f.Name.ToUpper().Contains(trimmed.ToUpper()));

    var onrcResults = await onrcQuery
 .OrderBy(f => f.Name)
   .Take(maxResults)
   .Select(f => new FirmLookupItem
   {
     CUI = f.CUI,
     Name = f.Name,
     TradeRegisterNo = f.TradeRegisterNo,
     Address = f.Address,
     Locality = f.Locality,
     County = f.County,
     Phone = f.Phone,
     Status = f.Status,
     CAEN = f.CAEN,
     Source = "ONRC",
   })
.ToListAsync(ct);

    return new FirmLookupResults
    {
      TenantCompanies = tenantResults,
      OnrcRecords = onrcResults,
      Region = region,
    };
  }

  public async Task<FirmLookupItem?> GetByCuiAsync(string cui, CancellationToken ct = default)
  {
    var region = await GetTenantRegionAsync(ct);
    var normalized = cui.Trim().TrimStart('R', 'O', 'r', 'o');

    var record = await _db.ONRCFirmRecords
            .IgnoreQueryFilters()
        .Where(f => f.Region == region && f.CUI == normalized)
   .Select(f => new FirmLookupItem
   {
     CUI = f.CUI,
     Name = f.Name,
     TradeRegisterNo = f.TradeRegisterNo,
     Address = f.Address,
     Locality = f.Locality,
     County = f.County,
     Phone = f.Phone,
     Status = f.Status,
     CAEN = f.CAEN,
     Source = "ONRC",
   })
            .FirstOrDefaultAsync(ct);

    return record;
  }

  private async Task<SystemRegion> GetTenantRegionAsync(CancellationToken ct)
  {
    if (!_currentUser.TenantId.HasValue)
      return SystemRegion.Romania;

    var tenant = await _db.Tenants
 .AsNoTracking()
.IgnoreQueryFilters()
        .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId.Value, ct);

    return tenant?.Region ?? SystemRegion.Romania;
  }
}
