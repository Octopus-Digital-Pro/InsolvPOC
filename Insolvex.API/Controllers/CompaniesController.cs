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
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CompanyView)]
public class CompaniesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CompaniesController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type = null)
    {
var query = _db.Companies.Include(c => c.AssignedTo).AsQueryable();

      if (type != null && Enum.TryParse<CompanyType>(type, true, out var companyType))
            query = query.Where(c => c.CompanyType == companyType);

        var companies = await query.OrderBy(c => c.Name).ToListAsync();

        var caseCounts = await _db.CaseParties
     .GroupBy(p => p.CompanyId)
   .Select(g => new { CompanyId = g.Key, Count = g.Select(p => p.CaseId).Distinct().Count() })
      .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

        var dtos = companies.Select(c => c.ToDto(caseCounts.GetValueOrDefault(c.Id, 0))).ToList();
        return Ok(dtos);
    }

 [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
      var company = await _db.Companies
            .Include(c => c.AssignedTo)
     .FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        var caseCount = await _db.CaseParties.Where(p => p.CompanyId == id).Select(p => p.CaseId).Distinct().CountAsync();
        return Ok(company.ToDto(caseCount));
    }

    [HttpPost]
    [RequirePermission(Permission.CompanyCreate)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request)
    {
        var companyType = CompanyType.Debtor;
        if (request.CompanyType != null && Enum.TryParse<CompanyType>(request.CompanyType, true, out var ct))
       companyType = ct;

var company = new Company
        {
  Id = Guid.NewGuid(),
            Name = request.Name,
            CompanyType = companyType,
       CuiRo = request.CuiRo,
            TradeRegisterNo = request.TradeRegisterNo,
VatNumber = request.VatNumber,
            Address = request.Address,
            Locality = request.Locality,
            County = request.County,
     Country = request.Country,
     PostalCode = request.PostalCode,
            Caen = request.Caen,
            IncorporationYear = request.IncorporationYear,
   ShareCapitalRon = request.ShareCapitalRon,
        Phone = request.Phone,
   Email = request.Email,
 ContactPerson = request.ContactPerson,
        Iban = request.Iban,
BankName = request.BankName,
      };

        _db.Companies.Add(company);
  await _db.SaveChangesAsync();
        await _audit.LogAsync("Company.Created", company.Id);
        return CreatedAtAction(nameof(GetById), new { id = company.Id }, company.ToDto());
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.CompanyEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyRequest request)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        if (request.Name != null) company.Name = request.Name;
        if (request.CompanyType != null && Enum.TryParse<CompanyType>(request.CompanyType, true, out var ct))
      company.CompanyType = ct;
if (request.CuiRo != null) company.CuiRo = request.CuiRo;
        if (request.TradeRegisterNo != null) company.TradeRegisterNo = request.TradeRegisterNo;
        if (request.VatNumber != null) company.VatNumber = request.VatNumber;
        if (request.Address != null) company.Address = request.Address;
        if (request.Locality != null) company.Locality = request.Locality;
        if (request.County != null) company.County = request.County;
     if (request.Country != null) company.Country = request.Country;
        if (request.PostalCode != null) company.PostalCode = request.PostalCode;
        if (request.Caen != null) company.Caen = request.Caen;
        if (request.IncorporationYear != null) company.IncorporationYear = request.IncorporationYear;
        if (request.ShareCapitalRon.HasValue) company.ShareCapitalRon = request.ShareCapitalRon;
        if (request.Phone != null) company.Phone = request.Phone;
    if (request.Email != null) company.Email = request.Email;
        if (request.ContactPerson != null) company.ContactPerson = request.ContactPerson;
        if (request.Iban != null) company.Iban = request.Iban;
    if (request.BankName != null) company.BankName = request.BankName;
        if (request.AssignedToUserId.HasValue) company.AssignedToUserId = request.AssignedToUserId;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Company.Updated", company.Id);
        return Ok(company.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.CompanyDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

   _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Company.Deleted", id);
        return NoContent();
    }
}
