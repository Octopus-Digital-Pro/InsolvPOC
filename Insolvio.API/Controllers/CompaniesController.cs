using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;
namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CompanyView)]
public class CompaniesController : ControllerBase
{
  private readonly ICompanyService _companies;

  public CompaniesController(ICompanyService companies) => _companies = companies;

  [HttpGet]
  public async Task<IActionResult> GetAll(CancellationToken ct)
      => Ok(await _companies.GetAllAsync(ct));

  /// <summary>Search companies by name, CUI, or trade register number.</summary>
  [HttpGet("search")]
  public async Task<IActionResult> Search(
      [FromQuery] string q,
      [FromQuery] int maxResults = 10,
      CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(q))
      return BadRequest(new { message = "Query parameter 'q' is required." });

    var results = await _companies.SearchAsync(q.Trim(), Math.Min(maxResults, 50), ct);
    return Ok(results);
  }

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
      Name = body.Name,
      CuiRo = body.CuiRo,
      TradeRegisterNo = body.TradeRegisterNo,
      VatNumber = body.VatNumber,
      Address = body.Address,
      Locality = body.Locality,
      County = body.County,
      Country = body.Country,
      PostalCode = body.PostalCode,
      Caen = body.Caen,
      IncorporationYear = body.IncorporationYear,
      ShareCapitalRon = body.ShareCapitalRon,
      Phone = body.Phone,
      Email = body.Email,
      ContactPerson = body.ContactPerson,
      Iban = body.Iban,
      BankName = body.BankName,
    }, ct);
    return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
  }

  [HttpPut("{id:guid}")]
  [RequirePermission(Permission.CompanyEdit)]
  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyBody body, CancellationToken ct)
  {
    var dto = await _companies.UpdateAsync(id, new UpdateCompanyCommand
    {
      Name = body.Name,
      CuiRo = body.CuiRo,
      TradeRegisterNo = body.TradeRegisterNo,
      VatNumber = body.VatNumber,
      Address = body.Address,
      Locality = body.Locality,
      County = body.County,
      Country = body.Country,
      PostalCode = body.PostalCode,
      Caen = body.Caen,
      IncorporationYear = body.IncorporationYear,
      ShareCapitalRon = body.ShareCapitalRon,
      Phone = body.Phone,
      Email = body.Email,
      ContactPerson = body.ContactPerson,
      Iban = body.Iban,
      BankName = body.BankName,
      AssignedToUserId = body.AssignedToUserId,
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

  /// <summary>Export all companies for the current tenant to CSV.</summary>
  [HttpGet("export-csv")]
  public async Task<IActionResult> ExportCsv(CancellationToken ct)
  {
    var bytes = await _companies.ExportCsvAsync(ct);
    return File(bytes, "text/csv", $"companies_{DateTime.UtcNow:yyyyMMdd}.csv");
  }
}

public record CreateCompanyBody(
    string Name, string? CuiRo = null, string? TradeRegisterNo = null,
    string? VatNumber = null, string? Address = null, string? Locality = null, string? County = null,
    string? Country = null, string? PostalCode = null, string? Caen = null, string? IncorporationYear = null,
    decimal? ShareCapitalRon = null, string? Phone = null, string? Email = null, string? ContactPerson = null,
  string? Iban = null, string? BankName = null);

public record UpdateCompanyBody(
    string? Name = null, string? CuiRo = null, string? TradeRegisterNo = null,
    string? VatNumber = null, string? Address = null, string? Locality = null, string? County = null,
 string? Country = null, string? PostalCode = null, string? Caen = null, string? IncorporationYear = null,
  decimal? ShareCapitalRon = null, string? Phone = null, string? Email = null, string? ContactPerson = null,
    string? Iban = null, string? BankName = null, Guid? AssignedToUserId = null);
