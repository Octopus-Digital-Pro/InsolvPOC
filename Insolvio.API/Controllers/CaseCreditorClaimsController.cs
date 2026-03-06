using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/claims")]
[Authorize]
[RequirePermission(Permission.PartyView)]
public class CaseCreditorClaimsController : ControllerBase
{
    private readonly ICreditorClaimsService _claims;

    public CaseCreditorClaimsController(ICreditorClaimsService claims) => _claims = claims;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _claims.GetAllAsync(caseId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid caseId, Guid id, CancellationToken ct)
        => Ok(await _claims.GetByIdAsync(caseId, id, ct));

    [HttpPost]
    [RequirePermission(Permission.PartyCreate)]
    public async Task<IActionResult> Create(Guid caseId, [FromBody] CreateCreditorClaimRequest req, CancellationToken ct)
        => Ok(await _claims.CreateAsync(caseId, req, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.PartyEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCreditorClaimRequest req, CancellationToken ct)
        => Ok(await _claims.UpdateAsync(caseId, id, req, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.PartyDelete)]
    public async Task<IActionResult> Delete(Guid caseId, Guid id, CancellationToken ct)
    {
        await _claims.DeleteAsync(caseId, id, ct);
        return Ok(new { message = "Deleted" });
    }
}
