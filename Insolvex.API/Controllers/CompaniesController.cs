using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
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
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CompaniesController(ICompanyService companies, ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _companies = companies;
        _db = db;
        _currentUser = currentUser;
    }

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

    /// <summary>Export all companies for the current tenant to CSV.</summary>
    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var companies = await _db.Companies
            .AsNoTracking()
            .Where(c => tenantId == null || c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.WriteRecords(companies.Select(c => new
        {
            c.Name,
            CompanyType = c.CompanyType.ToString(),
            c.CuiRo,
            c.TradeRegisterNo,
            c.VatNumber,
            c.Address,
            c.Locality,
            c.County,
            c.Country,
            c.PostalCode,
            c.Caen,
            c.IncorporationYear,
            c.ShareCapitalRon,
            c.Phone,
            c.Email,
            c.ContactPerson,
            c.Iban,
            c.BankName,
            CreatedOn = c.CreatedOn.ToString("yyyy-MM-dd"),
        }));

        var bytes = Encoding.UTF8.GetBytes(writer.ToString());
        return File(bytes, "text/csv", $"companies_{DateTime.UtcNow:yyyyMMdd}.csv");
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
