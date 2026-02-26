using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;

namespace Insolvex.Data.Services;

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

    public async Task CreateClientErrorAsync(CreateClientErrorLogRequest request, string? userId, string? userEmail, CancellationToken ct = default)
    {
        var sourceParts = new List<string> { "Frontend" };
        if (!string.IsNullOrWhiteSpace(request.Source)) sourceParts.Add(request.Source!);
        if (!string.IsNullOrWhiteSpace(request.UserAgent)) sourceParts.Add(request.UserAgent!);

        var contextPayload = new
        {
            request.AdditionalContext,
            userAgent = request.UserAgent
        };

        var errorLog = new Domain.Entities.ErrorLog
        {
            Id = Guid.NewGuid(),
            Message = request.Message,
            StackTrace = string.IsNullOrWhiteSpace(request.StackTrace) ? null : request.StackTrace,
            Source = string.Join(" | ", sourceParts),
            RequestPath = request.RequestPath,
            RequestMethod = "CLIENT",
            UserId = userId,
            UserEmail = userEmail,
            Timestamp = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = userEmail ?? "FrontendClient",
            IsResolved = false,
        };

        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            errorLog.StackTrace = string.IsNullOrWhiteSpace(errorLog.StackTrace)
                ? System.Text.Json.JsonSerializer.Serialize(contextPayload)
                : $"{errorLog.StackTrace}{Environment.NewLine}{System.Text.Json.JsonSerializer.Serialize(contextPayload)}";
        }

        _db.ErrorLogs.Add(errorLog);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var log = await _db.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("ErrorLog", id);
        log.IsResolved = true;
        await _db.SaveChangesAsync(ct);
    }
}
