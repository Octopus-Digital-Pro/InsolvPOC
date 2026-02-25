using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/parties")]
[Authorize]
[RequirePermission(Permission.PartyView)]
public class CasePartiesController : ControllerBase
{
    private readonly ICasePartyService _parties;

    public CasePartiesController(ICasePartyService parties) => _parties = parties;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _parties.GetAllAsync(caseId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid caseId, Guid id, CancellationToken ct)
    {
        var party = await _parties.GetByIdAsync(caseId, id, ct);
        if (party is null) return NotFound();
        return Ok(party);
    }

    [HttpPost]
    [RequirePermission(Permission.PartyCreate)]
    public async Task<IActionResult> Create(Guid caseId, [FromBody] CreateCasePartyRequest req, CancellationToken ct)
        => Ok(await _parties.CreateAsync(caseId, req, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.PartyEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCasePartyRequest req, CancellationToken ct)
    {
        var party = await _parties.UpdateAsync(caseId, id, req, ct);
        if (party is null) return NotFound();
        return Ok(party);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.PartyDelete)]
    public async Task<IActionResult> Delete(Guid caseId, Guid id, CancellationToken ct)
    {
        await _parties.DeleteAsync(caseId, id, ct);
        return Ok(new { message = "Deleted" });
    }
}
