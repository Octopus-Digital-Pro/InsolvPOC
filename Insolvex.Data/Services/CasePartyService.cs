using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

public sealed class CasePartyService : ICasePartyService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CasePartyService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<CasePartyDto>> GetAllAsync(Guid caseId, CancellationToken ct = default)
        => await _db.CaseParties
            .Include(p => p.Company)
            .Where(p => p.CaseId == caseId)
            .Select(p => p.ToDto())
            .ToListAsync(ct);

    public async Task<CasePartyDto> GetByIdAsync(Guid caseId, Guid partyId, CancellationToken ct = default)
    {
        var party = await _db.CaseParties
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.Id == partyId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CaseParty", partyId);
        return party.ToDto();
    }

    public async Task<CasePartyDto> CreateAsync(Guid caseId, CreateCasePartyRequest request, CancellationToken ct = default)
    {
        var party = new CaseParty
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            CompanyId = request.CompanyId,
            Role = Enum.Parse<CasePartyRole>(request.Role, true),
            RoleDescription = request.RoleDescription,
            ClaimAmountRon = request.ClaimAmountRon,
            ClaimAccepted = request.ClaimAccepted,
            JoinedDate = request.JoinedDate,
            Notes = request.Notes,
        };

        _db.CaseParties.Add(party);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(party).Reference(p => p.Company).LoadAsync(ct);
        await _audit.LogEntityAsync("Case Party Added", "CaseParty", party.Id,
            newValues: new { caseId, request.CompanyId, role = request.Role });

        return party.ToDto();
    }

    public async Task<CasePartyDto> UpdateAsync(Guid caseId, Guid partyId, UpdateCasePartyRequest request, CancellationToken ct = default)
    {
        var party = await _db.CaseParties
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.Id == partyId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CaseParty", partyId);

        var old = new { role = party.Role.ToString(), party.ClaimAmountRon, party.ClaimAccepted };

        if (request.Role != null) party.Role = Enum.Parse<CasePartyRole>(request.Role, true);
        if (request.RoleDescription != null) party.RoleDescription = request.RoleDescription;
        if (request.ClaimAmountRon.HasValue) party.ClaimAmountRon = request.ClaimAmountRon;
        if (request.ClaimAccepted.HasValue) party.ClaimAccepted = request.ClaimAccepted;
        if (request.Notes != null) party.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Case Party Updated", "CaseParty", party.Id,
            old, new { role = party.Role.ToString(), party.ClaimAmountRon, party.ClaimAccepted });

        return party.ToDto();
    }

    public async Task DeleteAsync(Guid caseId, Guid partyId, CancellationToken ct = default)
    {
        var party = await _db.CaseParties
            .FirstOrDefaultAsync(p => p.Id == partyId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CaseParty", partyId);

        await _audit.LogEntityAsync("Case Party Removed", "CaseParty", partyId,
            oldValues: new { caseId, companyId = party.CompanyId, role = party.Role.ToString() },
            severity: "Warning");
        _db.CaseParties.Remove(party);
        await _db.SaveChangesAsync(ct);
    }
}
