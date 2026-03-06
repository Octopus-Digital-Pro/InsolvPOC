using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class CreditorClaimsService : ICreditorClaimsService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CreditorClaimsService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<CreditorClaimDto>> GetAllAsync(Guid caseId, CancellationToken ct = default)
        => await _db.CreditorClaims
            .Include(c => c.CreditorParty).ThenInclude(p => p!.Company)
            .Where(c => c.CaseId == caseId)
            .OrderBy(c => c.RowNumber)
            .Select(c => c.ToDto())
            .ToListAsync(ct);

    public async Task<CreditorClaimDto> GetByIdAsync(Guid caseId, Guid claimId, CancellationToken ct = default)
    {
        var claim = await _db.CreditorClaims
            .Include(c => c.CreditorParty).ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(c => c.Id == claimId && c.CaseId == caseId, ct)
            ?? throw new NotFoundException("CreditorClaim", claimId);
        return claim.ToDto();
    }

    public async Task<CreditorClaimDto> CreateAsync(Guid caseId, CreateCreditorClaimRequest request, CancellationToken ct = default)
    {
        // Determine the next row number for this case
        var maxRow = await _db.CreditorClaims
            .Where(c => c.CaseId == caseId)
            .Select(c => (int?)c.RowNumber)
            .MaxAsync(ct) ?? 0;

        var claim = new CreditorClaim
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            CreditorPartyId = request.CreditorPartyId,
            RowNumber = maxRow + 1,
            DeclaredAmount = request.DeclaredAmount,
            Rank = request.Rank ?? "Chirographary",
            NatureDescription = request.NatureDescription,
            Status = request.Status ?? "Received",
            ReceivedAt = request.ReceivedAt,
            Notes = request.Notes,
        };

        _db.CreditorClaims.Add(claim);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(claim).Reference(c => c.CreditorParty).LoadAsync(ct);
        if (claim.CreditorParty != null)
            await _db.Entry(claim.CreditorParty).Reference(p => p.Company).LoadAsync(ct);

        await _audit.LogEntityAsync("Creditor Claim Added", "CreditorClaim", claim.Id,
            newValues: new { caseId, request.CreditorPartyId, request.DeclaredAmount, claim.Rank, claim.Status });

        return claim.ToDto();
    }

    public async Task<CreditorClaimDto> UpdateAsync(Guid caseId, Guid claimId, UpdateCreditorClaimRequest request, CancellationToken ct = default)
    {
        var claim = await _db.CreditorClaims
            .Include(c => c.CreditorParty).ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(c => c.Id == claimId && c.CaseId == caseId, ct)
            ?? throw new NotFoundException("CreditorClaim", claimId);

        var old = new { claim.DeclaredAmount, claim.AdmittedAmount, claim.Status, claim.Rank };

        if (request.DeclaredAmount.HasValue) claim.DeclaredAmount = request.DeclaredAmount.Value;
        if (request.AdmittedAmount.HasValue) claim.AdmittedAmount = request.AdmittedAmount;
        if (request.Rank != null) claim.Rank = request.Rank;
        if (request.NatureDescription != null) claim.NatureDescription = request.NatureDescription;
        if (request.Status != null) claim.Status = request.Status;
        if (request.ReceivedAt.HasValue) claim.ReceivedAt = request.ReceivedAt;
        if (request.Notes != null) claim.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Creditor Claim Updated", "CreditorClaim", claim.Id,
            old, new { claim.DeclaredAmount, claim.AdmittedAmount, claim.Status, claim.Rank });

        return claim.ToDto();
    }

    public async Task DeleteAsync(Guid caseId, Guid claimId, CancellationToken ct = default)
    {
        var claim = await _db.CreditorClaims
            .FirstOrDefaultAsync(c => c.Id == claimId && c.CaseId == caseId, ct)
            ?? throw new NotFoundException("CreditorClaim", claimId);

        await _audit.LogEntityAsync("Creditor Claim Removed", "CreditorClaim", claimId,
            oldValues: new { caseId, claim.CreditorPartyId, claim.DeclaredAmount },
            severity: "Warning");
        _db.CreditorClaims.Remove(claim);
        await _db.SaveChangesAsync(ct);
    }
}
