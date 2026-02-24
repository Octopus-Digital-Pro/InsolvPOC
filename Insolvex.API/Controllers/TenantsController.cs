using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.SystemConfigView)]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

 public TenantsController(ApplicationDbContext db)
    {
  _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _db.Tenants
  .IgnoreQueryFilters()
 .OrderBy(t => t.Name)
   .Select(t => new
   {
 t.Id,
    t.Name,
      t.Domain,
    t.IsActive,
       t.SubscriptionExpiry,
  t.PlanName,
     Region = t.Region.ToString(),
   t.CreatedOn,
  UserCount = _db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id),
      CompanyCount = _db.Companies.IgnoreQueryFilters().Count(c => c.TenantId == t.Id),
  CaseCount = _db.InsolvencyCases.IgnoreQueryFilters().Count(c => c.TenantId == t.Id),
 })
 .ToListAsync();
   return Ok(tenants);
  }

  [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();
        return Ok(tenant.ToDto());
}

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
      var tenant = new Tenant
  {
        Id = Guid.NewGuid(),
 Name = request.Name,
  Domain = request.Domain,
    PlanName = request.PlanName,
     IsActive = true,
   CreatedOn = DateTime.UtcNow,
         CreatedBy = "System"
        };

        if (request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var region))
            tenant.Region = region;

  _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();
   return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

     if (request.Name != null) tenant.Name = request.Name;
  if (request.Domain != null) tenant.Domain = request.Domain;
if (request.IsActive.HasValue) tenant.IsActive = request.IsActive.Value;
        if (request.PlanName != null) tenant.PlanName = request.PlanName;
   if (request.SubscriptionExpiry.HasValue) tenant.SubscriptionExpiry = request.SubscriptionExpiry.Value;
        if (request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var region))
  tenant.Region = region;

   await _db.SaveChangesAsync();
    return Ok(tenant.ToDto());
 }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
    var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        // Safety: don't delete if it has users
        var userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == id);
     if (userCount > 0)
       return BadRequest(new { message = $"Cannot delete tenant with {userCount} users. Remove users first." });

  _db.Tenants.Remove(tenant);
      await _db.SaveChangesAsync();
        return Ok(new { message = "Tenant deleted" });
    }
}
