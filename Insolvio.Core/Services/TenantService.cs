using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Services;

public sealed class TenantService : ITenantService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public TenantService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<TenantSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct);
        if (tenants.Count == 0) return [];

        // Run sequentially — DbContext does not support concurrent operations
        var userCounts = await _db.Users.AsNoTracking().IgnoreQueryFilters()
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var companyCounts = await _db.Companies.AsNoTracking().IgnoreQueryFilters()
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        var caseCounts = await _db.InsolvencyCases.AsNoTracking().IgnoreQueryFilters()
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        return tenants.Select(t => new TenantSummaryDto(
            t.Id, t.Name, t.Domain, t.IsActive,
            t.SubscriptionExpiry, t.PlanName, t.Region.ToString(),
            userCounts.GetValueOrDefault(t.Id, 0),
            companyCounts.GetValueOrDefault(t.Id, 0),
            caseCounts.GetValueOrDefault(t.Id, 0))).ToList();
    }

    public async Task<TenantDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tenant", id);
        return tenant.ToDto();
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Domain = request.Domain,
            PlanName = request.PlanName,
            Region = request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var reg) ? reg : SystemRegion.Romania,
            Language = request.Language ?? "en",
            IsActive = true,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Tenant Created", "Tenant", tenant.Id,
            newValues: new { tenant.Name, tenant.Domain, tenant.PlanName });
        return tenant.ToDto();
    }

    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tenant", id);

        var old = new { tenant.Name, tenant.Domain, tenant.IsActive, tenant.PlanName };

        if (request.Name != null) tenant.Name = request.Name;
        if (request.Domain != null) tenant.Domain = request.Domain;
        if (request.IsActive.HasValue) tenant.IsActive = request.IsActive.Value;
        if (request.PlanName != null) tenant.PlanName = request.PlanName;
        if (request.SubscriptionExpiry.HasValue) tenant.SubscriptionExpiry = request.SubscriptionExpiry;
        if (request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var reg))
            tenant.Region = reg;
        if (request.Language != null) tenant.Language = request.Language;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Tenant Updated", "Tenant", tenant.Id,
            old, new { tenant.Name, tenant.Domain, tenant.IsActive, tenant.PlanName });
        return tenant.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tenant", id);

        var userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, ct);
        if (userCount > 0)
            throw new BusinessException($"Cannot delete tenant with {userCount} active user(s). Deactivate users first.");

        await _audit.LogEntityAsync("Tenant Deleted", "Tenant", id,
            oldValues: new { tenant.Name }, severity: "Critical");
        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync(ct);
    }
}
