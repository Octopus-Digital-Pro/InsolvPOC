using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.ErrorLogView)]
public class ErrorLogsController : ControllerBase
{
  private readonly IErrorLogService _errors;

  public ErrorLogsController(IErrorLogService errors) => _errors = errors;

  [HttpGet]
  public async Task<IActionResult> GetAll([FromQuery] int page = 0, [FromQuery] int pageSize = 50,
      [FromQuery] bool? resolved = null, CancellationToken ct = default)
  {
    var (items, total) = await _errors.GetAllAsync(page, pageSize, resolved, ct);
    return Ok(new { items, total });
  }

  [HttpPut("{id:guid}/resolve")]
  public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
  {
    await _errors.ResolveAsync(id, ct);
    return Ok(new { message = "Marked as resolved" });
  }
}
