using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CompanyService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<CompanyDto>> GetAllAsync(string? type, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
      var query = _db.Companies
 .Include(c => c.AssignedTo)
 .Where(c => tenantId == null || c.TenantId == tenantId);

        if (type != null && Enum.TryParse<CompanyType>(type, true, out var companyType))
            query = query.Where(c => c.CompanyType == companyType);

   var companies = await query.OrderBy(c => c.Name).ToListAsync(ct);

        var caseCounts = await _db.CaseParties
          .GroupBy(p => p.CompanyId)
            .Select(g => new { g.Key, Count = g.Select(p => p.CaseId).Distinct().Count() })
     .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return companies.Select(c => c.ToDto(caseCounts.GetValueOrDefault(c.Id, 0))).ToList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
  var company = await _db.Companies
.Include(c => c.AssignedTo)
            .FirstOrDefaultAsync(c => c.Id == id && (tenantId == null || c.TenantId == tenantId), ct);
        if (company is null) return null;

      var caseCount = await _db.CaseParties.Where(p => p.CompanyId == id).Select(p => p.CaseId).Distinct().CountAsync(ct);
        return company.ToDto(caseCount);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context is required to create a company.");

  var companyType = CompanyType.Debtor;
     if (cmd.CompanyType != null && Enum.TryParse<CompanyType>(cmd.CompanyType, true, out var parsed))
 companyType = parsed;

        var company = new Company
        {
   Id = Guid.NewGuid(),
    TenantId = tenantId,
     Name = cmd.Name,
            CompanyType = companyType,
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
          Action = "Company.Created",
            Description = $"A new {companyType} company '{company.Name}' was registered.",
            EntityType = "Company",
      EntityId = company.Id,
    EntityName = company.Name,
            NewValues = new { company.Name, companyType, company.CuiRo },
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
        if (cmd.CompanyType != null && Enum.TryParse<CompanyType>(cmd.CompanyType, true, out var ct2))
       company.CompanyType = ct2;
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
            Action = "Company.Updated",
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
            Action = "Company.Deleted",
            Description = $"Company '{company.Name}' was permanently deleted.",
     EntityType = "Company",
    EntityId = id,
      EntityName = company.Name,
            Severity = "Critical",
   Category = "CompanyManagement",
    });
    }
}
