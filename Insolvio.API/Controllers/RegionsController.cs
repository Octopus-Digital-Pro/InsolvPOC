using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.Core.Abstractions;
using Insolvio.API.Authorization;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RegionsController : ControllerBase
{
    private readonly IRegionService _regionService;

    public RegionsController(IRegionService regionService)
    {
        _regionService = regionService;
    }

    /// <summary>Get all regions with their usage counts.</summary>
    [HttpGet]
    [RequirePermission(Permission.RegionView)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var regions = await _regionService.GetAllAsync(ct);
        return Ok(regions);
    }

    /// <summary>Create a new region.</summary>
    [HttpPost]
    [RequirePermission(Permission.RegionManage)]
    public async Task<IActionResult> Create([FromBody] CreateRegionBody body, CancellationToken ct)
    {
        var region = await _regionService.CreateAsync(body.Name, body.IsoCode, body.Flag, ct);
        return CreatedAtAction(nameof(GetAll), new { }, region);
    }

    /// <summary>Delete a region (only if usage count is 0).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.RegionManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _regionService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Set a region as the default (unsets the previous default).</summary>
    [HttpPatch("{id:guid}/set-default")]
    [RequirePermission(Permission.RegionManage)]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        var region = await _regionService.SetDefaultAsync(id, ct);
        return Ok(region);
    }
}

public record CreateRegionBody(string Name, string IsoCode, string Flag);
