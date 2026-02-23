using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/parties")]
[Authorize]
[RequirePermission(Permission.PartyView)]
public class CasePartiesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CasePartiesController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId)
    {
        var parties = await _db.CaseParties
            .Include(p => p.Company)
      .Where(p => p.CaseId == caseId)
  .OrderBy(p => p.Role)
    .ThenBy(p => p.Company!.Name)
        .ToListAsync();

        return Ok(parties.Select(p => p.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid caseId, Guid id)
    {
        var party = await _db.CaseParties
     .Include(p => p.Company)
   .FirstOrDefaultAsync(p => p.Id == id && p.CaseId == caseId);

        if (party == null) return NotFound();
        return Ok(party.ToDto());
    }

    [HttpPost]
    [RequirePermission(Permission.PartyCreate)]
    public async Task<IActionResult> Create(Guid caseId, [FromBody] CreateCasePartyRequest req)
    {
        var caseExists = await _db.InsolvencyCases.AnyAsync(c => c.Id == caseId);
        if (!caseExists) return NotFound(new { message = "Case not found" });

        var companyExists = await _db.Companies.AnyAsync(c => c.Id == req.CompanyId);
        if (!companyExists) return BadRequest(new { message = "Company not found" });

        if (!Enum.TryParse<CasePartyRole>(req.Role, true, out var role))
            return BadRequest(new { message = $"Invalid role: {req.Role}" });

        var party = new CaseParty
        {
            CaseId = caseId,
            CompanyId = req.CompanyId,
            Role = role,
            RoleDescription = req.RoleDescription,
            ClaimAmountRon = req.ClaimAmountRon,
            ClaimAccepted = req.ClaimAccepted,
            JoinedDate = req.JoinedDate ?? DateTime.UtcNow,
            Notes = req.Notes,
        };

        _db.CaseParties.Add(party);
        await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("Party.Created", "CaseParty", party.Id,
  newValues: new { caseId, req.CompanyId, role = req.Role, req.ClaimAmountRon });

        return Ok(party.ToDto());
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.PartyEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCasePartyRequest req)
    {
        var party = await _db.CaseParties
     .Include(p => p.Company)
         .FirstOrDefaultAsync(p => p.Id == id && p.CaseId == caseId);

        if (party == null) return NotFound();

        var oldValues = new { role = party.Role.ToString(), party.RoleDescription, party.ClaimAmountRon, party.ClaimAccepted };

        if (req.Role != null && Enum.TryParse<CasePartyRole>(req.Role, true, out var role))
     party.Role = role;
        if (req.RoleDescription != null) party.RoleDescription = req.RoleDescription;
     if (req.ClaimAmountRon.HasValue) party.ClaimAmountRon = req.ClaimAmountRon;
      if (req.ClaimAccepted.HasValue) party.ClaimAccepted = req.ClaimAccepted;
        if (req.Notes != null) party.Notes = req.Notes;

     await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("Party.Updated", "CaseParty", id, oldValues,
            new { role = party.Role.ToString(), party.RoleDescription, party.ClaimAmountRon, party.ClaimAccepted });
        return Ok(party.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.PartyDelete)]
    public async Task<IActionResult> Delete(Guid caseId, Guid id)
    {
   var party = await _db.CaseParties.FirstOrDefaultAsync(p => p.Id == id && p.CaseId == caseId);
    if (party == null) return NotFound();

        await _audit.LogEntityAsync("Party.Deleted", "CaseParty", id,
     oldValues: new { caseId, party.CompanyId, role = party.Role.ToString() }, severity: "Warning");

        _db.CaseParties.Remove(party);
        await _db.SaveChangesAsync();
return Ok(new { message = "Deleted" });
    }
}
