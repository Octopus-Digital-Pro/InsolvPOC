using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;

namespace Insolvex.Core.Services;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IApplicationDbContext _db;

    public AuditLogQueryService(IApplicationDbContext db) => _db = db;

    public async Task<(List<AuditLogDto> Items, int Total)> GetAllAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = ApplyFilters(_db.AuditLogs.AsQueryable(), filter);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip(filter.Page * filter.PageSize)
            .Take(filter.PageSize)
            .Select(l => l.ToDto())
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<List<AuditLogDto>> GetForExportAsync(AuditLogFilter filter, CancellationToken ct = default)
        => await ApplyFilters(_db.AuditLogs.AsQueryable(), filter)
            .OrderByDescending(l => l.Timestamp)
            .Take(50_000)
            .Select(l => l.ToDto())
            .ToListAsync(ct);

    public async Task<int> GetCountAsync(AuditLogFilter filter, CancellationToken ct = default)
        => await ApplyFilters(_db.AuditLogs.AsQueryable(), filter).CountAsync(ct);

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
        => await _db.AuditLogs.Select(l => l.Category).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<object> GetStatsAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var since = from ?? DateTime.UtcNow.AddDays(-30);
        var recent = _db.AuditLogs.Where(l => l.Timestamp >= since);
        if (to.HasValue) recent = recent.Where(l => l.Timestamp <= to.Value);
        return new
        {
            totalLast30Days = await recent.CountAsync(ct),
            criticalLast30Days = await recent.CountAsync(l => l.Severity == "Critical", ct),
            byCategory = await recent.GroupBy(l => l.Category)
                .Select(g => new { category = g.Key, count = g.Count() })
                .ToListAsync(ct),
            bySeverity = await recent.GroupBy(l => l.Severity)
                .Select(g => new { severity = g.Key, count = g.Count() })
                .ToListAsync(ct),
        };
    }

    private static IQueryable<Domain.Entities.AuditLog> ApplyFilters(
        IQueryable<Domain.Entities.AuditLog> q, AuditLogFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Action)) q = q.Where(l => l.Action.Contains(f.Action));
        if (f.UserId.HasValue) q = q.Where(l => l.UserId == f.UserId);
        if (!string.IsNullOrWhiteSpace(f.EntityType)) q = q.Where(l => l.EntityType == f.EntityType);
        if (f.EntityId.HasValue) q = q.Where(l => l.EntityId == f.EntityId);
        if (!string.IsNullOrWhiteSpace(f.Severity)) q = q.Where(l => l.Severity == f.Severity);
        if (!string.IsNullOrWhiteSpace(f.Category)) q = q.Where(l => l.Category == f.Category);
        if (f.FromDate.HasValue) q = q.Where(l => l.Timestamp >= f.FromDate.Value);
        if (f.ToDate.HasValue) q = q.Where(l => l.Timestamp <= f.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
            q = q.Where(l => l.Action.Contains(f.Search) ||
                              l.Description.Contains(f.Search) ||
                              (l.UserEmail != null && l.UserEmail.Contains(f.Search)));
        return q;
    }
}
