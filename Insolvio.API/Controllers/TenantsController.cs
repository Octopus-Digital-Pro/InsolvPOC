using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.SystemConfigView)]
public class TenantsController : ControllerBase
{
  private readonly ITenantService _tenants;

  public TenantsController(ITenantService tenants) => _tenants = tenants;

  [HttpGet]
  public async Task<IActionResult> GetAll(CancellationToken ct)
      => Ok(await _tenants.GetAllAsync(ct));

  [HttpGet("{id:guid}")]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var tenant = await _tenants.GetByIdAsync(id, ct);
    if (tenant is null) return NotFound();
    return Ok(tenant);
  }

  [HttpPost]
  public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
  {
    var tenant = await _tenants.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
  {
    var tenant = await _tenants.UpdateAsync(id, request, ct);
    if (tenant is null) return NotFound();
    return Ok(tenant);
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    await _tenants.DeleteAsync(id, ct);
    return Ok(new { message = "Tenant deleted" });
  }
}
