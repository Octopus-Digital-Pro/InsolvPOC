using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.ErrorLogView)]
public class ErrorLogsController : ControllerBase
{
 private readonly ApplicationDbContext _db;

    public ErrorLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 0, [FromQuery] int pageSize = 50)
    {
     var logs = await _db.ErrorLogs
      .IgnoreQueryFilters()
    .OrderByDescending(l => l.Timestamp)
      .Skip(page * pageSize)
 .Take(pageSize)
   .Select(l => l.ToDto())
     .ToListAsync();

      return Ok(logs);
    }

 [HttpPut("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id)
    {
        var log = await _db.ErrorLogs.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == id);
   if (log == null) return NotFound();

     log.IsResolved = true;
  await _db.SaveChangesAsync();
        return Ok(log.ToDto());
    }
}
