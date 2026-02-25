using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;

namespace Insolvex.API.Services;

public sealed class ErrorLogService : IErrorLogService
{
    private readonly ApplicationDbContext _db;

    public ErrorLogService(ApplicationDbContext db) => _db = db;

    public async Task<(List<ErrorLogDto> Items, int Total)> GetAllAsync(
        int page = 0, int pageSize = 50, bool? resolved = null, CancellationToken ct = default)
    {
        var query = _db.ErrorLogs.AsQueryable();
        if (resolved.HasValue) query = query.Where(e => e.IsResolved == resolved.Value);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(e => e.ToDto())
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var log = await _db.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("ErrorLog", id);
        log.IsResolved = true;
        await _db.SaveChangesAsync(ct);
    }
}
