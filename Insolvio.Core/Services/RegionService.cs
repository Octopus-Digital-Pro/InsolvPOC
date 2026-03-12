using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Services;

/// <summary>
/// Manages global country/region records. Only Global Admins may mutate regions.
/// Usage count reflects the number of tenants currently assigned to each region.
/// </summary>
public sealed class RegionService : IRegionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RegionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<RegionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var regions = await _db.Regions
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        // Count tenants per SystemRegion enum value, mapped by name.
        // Tenant.Region is stored as int (SystemRegion enum). We fetch all counts grouped by enum.
        var tenantCounts = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .GroupBy(t => t.Region)
            .Select(g => new { Region = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Map enum string name -> count
        var countMap = tenantCounts.ToDictionary(
            x => x.Region.ToString(),
            x => x.Count,
            StringComparer.OrdinalIgnoreCase);

        return regions
            .Select(r => new RegionDto(
                r.Id,
                r.Name,
                r.IsoCode,
                r.Flag,
                countMap.TryGetValue(r.Name, out var c) ? c : 0,
                r.IsDefault))
            .ToList();
    }

    public async Task<RegionDto> CreateAsync(string name, string isoCode, string flag, CancellationToken ct = default)
    {
        if (!_currentUser.IsGlobalAdmin)
            throw new ForbiddenException("Only Global Admins can create regions.");

        name = name.Trim();
        isoCode = isoCode.Trim().ToUpperInvariant();

        if (await _db.Regions.AnyAsync(r => r.Name == name || r.IsoCode == isoCode, ct))
            throw new BusinessException($"A region with name '{name}' or ISO code '{isoCode}' already exists.");

        var region = new Region
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsoCode = isoCode,
            Flag = flag.Trim(),
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };

        _db.Regions.Add(region);
        await _db.SaveChangesAsync(ct);

        return new RegionDto(region.Id, region.Name, region.IsoCode, region.Flag, 0, region.IsDefault);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.IsGlobalAdmin)
            throw new ForbiddenException("Only Global Admins can delete regions.");

        var region = await _db.Regions.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Region {id} not found.");

        // Check usage — count tenants whose SystemRegion enum name matches this region's name.
        var usageCount = 0;
        if (Enum.TryParse<SystemRegion>(region.Name, true, out var enumVal))
        {
            usageCount = await _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .CountAsync(t => t.Region == enumVal, ct);
        }

        if (usageCount > 0)
            throw new BusinessException($"Region '{region.Name}' cannot be deleted because it is currently in use by {usageCount} tenant(s).");

        _db.Regions.Remove(region);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RegionDto> SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.IsGlobalAdmin)
            throw new ForbiddenException("Only Global Admins can change the default region.");

        var target = await _db.Regions.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Region {id} not found.");

        // Clear all existing defaults, then set the new one.
        var allRegions = await _db.Regions.ToListAsync(ct);
        foreach (var r in allRegions)
            r.IsDefault = r.Id == id;

        await _db.SaveChangesAsync(ct);

        // Return with an approximate usage count (no need for the full lookup here).
        var usageCount = 0;
        if (Enum.TryParse<SystemRegion>(target.Name, true, out var enumVal))
        {
            usageCount = await _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .CountAsync(t => t.Region == enumVal, ct);
        }

        return new RegionDto(target.Id, target.Name, target.IsoCode, target.Flag, usageCount, true);
    }
}
