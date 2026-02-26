using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CompanyView)]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companies;

    public CompaniesController(ICompanyService companies) => _companies = companies;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type, CancellationToken ct)
        => Ok(await _companies.GetAllAsync(type, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _companies.GetByIdAsync(id, ct);
      return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequirePermission(Permission.CompanyCreate)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyBody body, CancellationToken ct)
    {
        var dto = await _companies.CreateAsync(new CreateCompanyCommand
        {
            Name = body.Name, CompanyType = body.CompanyType, CuiRo = body.CuiRo,
       TradeRegisterNo = body.TradeRegisterNo, VatNumber = body.VatNumber,
  Address = body.Address, Locality = body.Locality, County = body.County,
    Country = body.Country, PostalCode = body.PostalCode, Caen = body.Caen,
            IncorporationYear = body.IncorporationYear, ShareCapitalRon = body.ShareCapitalRon,
     Phone = body.Phone, Email = body.Email, ContactPerson = body.ContactPerson,
   Iban = body.Iban, BankName = body.BankName,
        }, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.CompanyEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyBody body, CancellationToken ct)
  {
     var dto = await _companies.UpdateAsync(id, new UpdateCompanyCommand
        {
   Name = body.Name, CompanyType = body.CompanyType, CuiRo = body.CuiRo,
            TradeRegisterNo = body.TradeRegisterNo, VatNumber = body.VatNumber,
       Address = body.Address, Locality = body.Locality, County = body.County,
            Country = body.Country, PostalCode = body.PostalCode, Caen = body.Caen,
            IncorporationYear = body.IncorporationYear, ShareCapitalRon = body.ShareCapitalRon,
            Phone = body.Phone, Email = body.Email, ContactPerson = body.ContactPerson,
        Iban = body.Iban, BankName = body.BankName, AssignedToUserId = body.AssignedToUserId,
        }, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.CompanyDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _companies.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record CreateCompanyBody(
    string Name, string? CompanyType = null, string? CuiRo = null, string? TradeRegisterNo = null,
    string? VatNumber = null, string? Address = null, string? Locality = null, string? County = null,
    string? Country = null, string? PostalCode = null, string? Caen = null, string? IncorporationYear = null,
    decimal? ShareCapitalRon = null, string? Phone = null, string? Email = null, string? ContactPerson = null,
  string? Iban = null, string? BankName = null);

public record UpdateCompanyBody(
    string? Name = null, string? CompanyType = null, string? CuiRo = null, string? TradeRegisterNo = null,
    string? VatNumber = null, string? Address = null, string? Locality = null, string? County = null,
 string? Country = null, string? PostalCode = null, string? Caen = null, string? IncorporationYear = null,
  decimal? ShareCapitalRon = null, string? Phone = null, string? Email = null, string? ContactPerson = null,
    string? Iban = null, string? BankName = null, Guid? AssignedToUserId = null);
