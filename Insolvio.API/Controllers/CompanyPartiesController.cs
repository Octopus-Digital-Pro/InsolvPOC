using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

/// <summary>Exposes case-party information from the company perspective.</summary>
[ApiController]
[Route("api/companies/{companyId:guid}/parties")]
[Authorize]
[RequirePermission(Permission.PartyView)]
public class CompanyPartiesController : ControllerBase
{
    private readonly ICasePartyService _parties;

    public CompanyPartiesController(ICasePartyService parties) => _parties = parties;

    [HttpGet]
    public async Task<IActionResult> GetByCompany(Guid companyId, CancellationToken ct)
        => Ok(await _parties.GetByCompanyAsync(companyId, ct));
}
