using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.AuditLogView)]
public class AuditLogsController : ControllerBase
{
  private readonly IAuditLogQueryService _logs;

  public AuditLogsController(IAuditLogQueryService logs) => _logs = logs;

  [HttpGet]
  public async Task<IActionResult> GetAll([FromQuery] AuditLogFilter filter, CancellationToken ct)
  {
    var (items, total) = await _logs.GetAllAsync(filter, ct);
    return Ok(new { items, total, page = filter.Page, pageSize = filter.PageSize });
  }

  [HttpGet("export")]
  public async Task<IActionResult> Export([FromQuery] AuditLogFilter filter, CancellationToken ct)
  {
    var items = await _logs.GetForExportAsync(filter with { Page = 0, PageSize = 50_000 }, ct);

    var csv = new StringBuilder();
    csv.AppendLine("Timestamp,Severity,Category,Action,UserEmail,EntityType,EntityId,RequestMethod,RequestPath,ResponseStatusCode,DurationMs,Description");

    foreach (var log in items)
    {
      csv.AppendLine(string.Join(",", new[]
      {
        CsvEscape(log.Timestamp.ToString("o")),
        CsvEscape(log.Severity),
        CsvEscape(log.Category),
        CsvEscape(log.Action),
        CsvEscape(log.UserEmail),
        CsvEscape(log.EntityType),
        CsvEscape(log.EntityId?.ToString()),
        CsvEscape(log.RequestMethod),
        CsvEscape(log.RequestPath),
        CsvEscape(log.ResponseStatusCode?.ToString()),
        CsvEscape(log.DurationMs?.ToString()),
        CsvEscape(log.Description),
      }));
    }

    var bytes = Encoding.UTF8.GetBytes(csv.ToString());
    var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
    return File(bytes, "text/csv", fileName);
  }

  [HttpGet("count")]
  public async Task<IActionResult> GetCount([FromQuery] AuditLogFilter filter, CancellationToken ct)
      => Ok(new { count = await _logs.GetCountAsync(filter, ct) });

  [HttpGet("categories")]
  public async Task<IActionResult> GetCategories(CancellationToken ct)
      => Ok(await _logs.GetCategoriesAsync(ct));

  [HttpGet("stats")]
  public async Task<IActionResult> GetStats([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
      => Ok(await _logs.GetStatsAsync(from, to, ct));

  private static string CsvEscape(string? value)
  {
    if (string.IsNullOrEmpty(value)) return string.Empty;
    return $"\"{value.Replace("\"", "\"\"") }\"";
  }
}
