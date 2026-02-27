using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/assets")]
[Authorize]
[RequirePermission(Permission.AssetView)]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assets;

    public AssetsController(IAssetService assets) => _assets = assets;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _assets.GetAllAsync(caseId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid caseId, Guid id, CancellationToken ct)
    {
        var asset = await _assets.GetByIdAsync(caseId, id, ct);
        if (asset is null) return NotFound();
        return Ok(asset);
    }

    [HttpPost]
    [RequirePermission(Permission.AssetCreate)]
    public async Task<IActionResult> Create(Guid caseId, [FromBody] CreateAssetRequest req, CancellationToken ct)
        => Ok(await _assets.CreateAsync(caseId, req, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.AssetEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateAssetRequest req, CancellationToken ct)
    {
        var asset = await _assets.UpdateAsync(caseId, id, req, ct);
        if (asset is null) return NotFound();
        return Ok(asset);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.AssetDelete)]
    public async Task<IActionResult> Delete(Guid caseId, Guid id, CancellationToken ct)
    {
        await _assets.DeleteAsync(caseId, id, ct);
        return Ok(new { message = "Deleted" });
    }
}
