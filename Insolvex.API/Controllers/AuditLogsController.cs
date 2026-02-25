using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    var (items, _) = await _logs.GetAllAsync(filter, ct);
    return Ok(items);
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
}
