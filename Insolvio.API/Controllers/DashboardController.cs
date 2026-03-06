using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.DashboardView)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
   => Ok(await _dashboard.GetDashboardAsync(ct));
}
