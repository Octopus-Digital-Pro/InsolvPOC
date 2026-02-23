using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.AuditLogView)]
public class AuditLogsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
  public async Task<IActionResult> GetAll([FromQuery] AuditLogFilter filter)
    {
        var query = ApplyFilters(_db.AuditLogs.AsQueryable(), filter);

        var logs = await query
  .OrderByDescending(l => l.Timestamp)
       .Skip(filter.Page * filter.PageSize)
            .Take(filter.PageSize)
    .Select(l => l.ToDto())
     .ToListAsync();

        return Ok(logs);
    }

  [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] AuditLogFilter filter)
    {
      var query = ApplyFilters(_db.AuditLogs.AsQueryable(), filter);
        var count = await query.CountAsync();
        return Ok(new { count });
    }

    /// <summary>Get distinct categories for filter dropdowns.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
  var categories = await _db.AuditLogs
            .Select(l => l.Category)
            .Distinct()
     .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    /// <summary>Get audit stats grouped by category and severity.</summary>
    [HttpGet("stats")]
  public async Task<IActionResult> GetStats([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
     var query = _db.AuditLogs.AsQueryable();
     if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);

        var byCategory = await query
            .GroupBy(l => l.Category)
   .Select(g => new { category = g.Key, count = g.Count() })
        .OrderByDescending(x => x.count)
   .ToListAsync();

  var bySeverity = await query
            .GroupBy(l => l.Severity)
        .Select(g => new { severity = g.Key, count = g.Count() })
            .ToListAsync();

        var total = await query.CountAsync();

        return Ok(new { total, byCategory, bySeverity });
    }

    private static IQueryable<Domain.Entities.AuditLog> ApplyFilters(
        IQueryable<Domain.Entities.AuditLog> query, AuditLogFilter filter)
    {
        if (filter.UserId.HasValue)
query = query.Where(l => l.UserId == filter.UserId);
        if (!string.IsNullOrEmpty(filter.Action))
          query = query.Where(l => l.Action.Contains(filter.Action));
        if (!string.IsNullOrEmpty(filter.EntityType))
       query = query.Where(l => l.EntityType != null && l.EntityType == filter.EntityType);
        if (filter.EntityId.HasValue)
  query = query.Where(l => l.EntityId == filter.EntityId);
   if (!string.IsNullOrEmpty(filter.Severity))
 query = query.Where(l => l.Severity == filter.Severity);
if (!string.IsNullOrEmpty(filter.Category))
            query = query.Where(l => l.Category == filter.Category);
     if (!string.IsNullOrEmpty(filter.Search))
     query = query.Where(l =>
          l.Action.Contains(filter.Search) ||
                (l.UserEmail != null && l.UserEmail.Contains(filter.Search)) ||
        (l.Changes != null && l.Changes.Contains(filter.Search)) ||
        (l.RequestPath != null && l.RequestPath.Contains(filter.Search)));
        if (filter.FromDate.HasValue)
            query = query.Where(l => l.Timestamp >= filter.FromDate);
 if (filter.ToDate.HasValue)
          query = query.Where(l => l.Timestamp <= filter.ToDate);
        return query;
    }
}
