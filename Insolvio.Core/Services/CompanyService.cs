using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;
namespace Insolvio.Core.Services;

public sealed class CompanyService : ICompanyService
{
  private readonly IApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;
  private readonly IAuditService _audit;

  public CompanyService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
  {
    _db = db;
    _currentUser = currentUser;
    _audit = audit;
  }

  public async Task<List<CompanyDto>> GetAllAsync(CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
    var query = _db.Companies
.Include(c => c.AssignedTo)
.Where(c => tenantId == null || c.TenantId == tenantId);

    var companies = await query.OrderBy(c => c.Name).ToListAsync(ct);

    var companyIds = companies.Select(c => c.Id).ToList();

    var caseNumbersByCompany = await _db.CaseParties
      .Where(p => companyIds.Contains(p.CompanyId))
      .Join(_db.InsolvencyCases, p => p.CaseId, c => c.Id, (p, c) => new { p.CompanyId, c.CaseNumber })
      .GroupBy(x => x.CompanyId)
      .ToDictionaryAsync(
        g => g.Key,
        g => g.Select(x => x.CaseNumber).Distinct().ToList(),
        ct);

    return companies.Select(c =>
    {
      var nums = caseNumbersByCompany.GetValueOrDefault(c.Id);
      return c.ToDto(nums?.Count ?? 0, nums);
    }).ToList();
  }

  public async Task<List<CompanyDto>> SearchAsync(string query, int maxResults, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
    var q = query.ToLowerInvariant();

    var matched = await _db.Companies
      .Include(c => c.AssignedTo)
      .Where(c => tenantId == null || c.TenantId == tenantId)
      .Where(c =>
        c.Name.ToLower().Contains(q) ||
        (c.CuiRo != null && c.CuiRo.ToLower().Contains(q)) ||
        (c.TradeRegisterNo != null && c.TradeRegisterNo.ToLower().Contains(q)))
      .OrderBy(c => c.Name)
      .Take(maxResults)
      .ToListAsync(ct);

    var companyIds = matched.Select(c => c.Id).ToList();
    var caseNumbersByCompany = await _db.CaseParties
      .Where(p => companyIds.Contains(p.CompanyId))
      .Join(_db.InsolvencyCases, p => p.CaseId, c => c.Id, (p, c) => new { p.CompanyId, c.CaseNumber })
      .GroupBy(x => x.CompanyId)
      .ToDictionaryAsync(
        g => g.Key,
        g => g.Select(x => x.CaseNumber).Distinct().ToList(),
        ct);

    return matched.Select(c =>
    {
      var nums = caseNumbersByCompany.GetValueOrDefault(c.Id);
      return c.ToDto(nums?.Count ?? 0, nums);
    }).ToList();
  }

  public async Task<CompanyDto?> GetByIdAsync(Guid id, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
    var company = await _db.Companies
  .Include(c => c.AssignedTo)
              .FirstOrDefaultAsync(c => c.Id == id && (tenantId == null || c.TenantId == tenantId), ct);
    if (company is null) return null;

    var caseNumbers = await _db.CaseParties
      .Where(p => p.CompanyId == id)
      .Join(_db.InsolvencyCases, p => p.CaseId, c => c.Id, (p, c) => c.CaseNumber)
      .Distinct()
      .ToListAsync(ct);
    return company.ToDto(caseNumbers.Count, caseNumbers);
  }

  public async Task<CompanyDto> CreateAsync(CreateCompanyCommand cmd, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId
        ?? throw new BusinessException("Tenant context is required to create a company.");

    var company = new Company
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      Name = cmd.Name,
      CuiRo = cmd.CuiRo,
      TradeRegisterNo = cmd.TradeRegisterNo,
      VatNumber = cmd.VatNumber,
      Address = cmd.Address,
      Locality = cmd.Locality,
      County = cmd.County,
      Country = cmd.Country,
      PostalCode = cmd.PostalCode,
      Caen = cmd.Caen,
      IncorporationYear = cmd.IncorporationYear,
      ShareCapitalRon = cmd.ShareCapitalRon,
      Phone = cmd.Phone,
      Email = cmd.Email,
      ContactPerson = cmd.ContactPerson,
      Iban = cmd.Iban,
      BankName = cmd.BankName,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = _currentUser.Email ?? "System",
    };

    _db.Companies.Add(company);
    await _db.SaveChangesAsync(ct);

    await _audit.LogAsync(new AuditEntry
    {
      Action = "Company Registered",
      Description = $"A new company '{company.Name}' was registered.",
      EntityType = "Company",
      EntityId = company.Id,
      EntityName = company.Name,
      NewValues = new { company.Name, company.CuiRo },
      Severity = "Info",
      Category = "CompanyManagement",
    });

    return company.ToDto();
  }

  public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyCommand cmd, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
    var company = await _db.Companies
.FirstOrDefaultAsync(c => c.Id == id && (tenantId == null || c.TenantId == tenantId), ct)
        ?? throw new BusinessException($"Company {id} not found.");

    if (cmd.Name != null) company.Name = cmd.Name;
    if (cmd.CuiRo != null) company.CuiRo = cmd.CuiRo;
    if (cmd.TradeRegisterNo != null) company.TradeRegisterNo = cmd.TradeRegisterNo;
    if (cmd.VatNumber != null) company.VatNumber = cmd.VatNumber;
    if (cmd.Address != null) company.Address = cmd.Address;
    if (cmd.Locality != null) company.Locality = cmd.Locality;
    if (cmd.County != null) company.County = cmd.County;
    if (cmd.Country != null) company.Country = cmd.Country;
    if (cmd.PostalCode != null) company.PostalCode = cmd.PostalCode;
    if (cmd.Caen != null) company.Caen = cmd.Caen;
    if (cmd.IncorporationYear != null) company.IncorporationYear = cmd.IncorporationYear;
    if (cmd.ShareCapitalRon.HasValue) company.ShareCapitalRon = cmd.ShareCapitalRon;
    if (cmd.Phone != null) company.Phone = cmd.Phone;
    if (cmd.Email != null) company.Email = cmd.Email;
    if (cmd.ContactPerson != null) company.ContactPerson = cmd.ContactPerson;
    if (cmd.Iban != null) company.Iban = cmd.Iban;
    if (cmd.BankName != null) company.BankName = cmd.BankName;
    if (cmd.AssignedToUserId.HasValue) company.AssignedToUserId = cmd.AssignedToUserId;

    company.LastModifiedOn = DateTime.UtcNow;
    company.LastModifiedBy = _currentUser.Email;

    await _db.SaveChangesAsync(ct);

    await _audit.LogAsync(new AuditEntry
    {
      Action = "Company Details Updated",
      Description = $"Company '{company.Name}' details were updated.",
      EntityType = "Company",
      EntityId = company.Id,
      EntityName = company.Name,
      Severity = "Info",
      Category = "CompanyManagement",
    });

    return company.ToDto();
  }

  public async Task DeleteAsync(Guid id, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
    var company = await _db.Companies
   .FirstOrDefaultAsync(c => c.Id == id && (tenantId == null || c.TenantId == tenantId), ct)
 ?? throw new BusinessException($"Company {id} not found.");

    _db.Companies.Remove(company);
    await _db.SaveChangesAsync(ct);

    await _audit.LogAsync(new AuditEntry
    {
      Action = "Company Deleted",
      Description = $"Company '{company.Name}' was permanently deleted.",
      EntityType = "Company",
      EntityId = id,
      EntityName = company.Name,
      Severity = "Critical",
      Category = "CompanyManagement",
    });
  }

  public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
  {
    var tenantId = _currentUser.TenantId;
    var companies = await _db.Companies
        .AsNoTracking()
        .Where(c => tenantId == null || c.TenantId == tenantId)
        .OrderBy(c => c.Name)
        .ToListAsync(ct);

    using var writer = new StringWriter();
    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    csv.WriteRecords(companies.Select(c => new
    {
      c.Name,
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
      ShareCapitalRon = c.ShareCapitalRon?.ToString("F2") ?? "",
      c.Phone,
      c.Email,
      c.ContactPerson,
      c.Iban,
      c.BankName,
      CreatedOn = c.CreatedOn.ToString("yyyy-MM-dd"),
    }));

    await _audit.LogAsync(new AuditEntry
    {
      Action = "Companies Exported to CSV",
      Description = $"Exported {companies.Count} company records.",
      EntityType = "Company",
      Severity = "Info",
      Category = "CompanyManagement",
    });

    return Encoding.UTF8.GetBytes(writer.ToString());
  }
}
