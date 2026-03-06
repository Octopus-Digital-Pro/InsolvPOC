using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class AssetService : IAssetService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AssetService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<AssetDto>> GetAllAsync(Guid caseId, CancellationToken ct = default)
        => await _db.Assets
            .Include(a => a.SecuredCreditorParty)
            .Where(a => a.CaseId == caseId)
            .OrderByDescending(a => a.CreatedOn)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

    public async Task<AssetDto> GetByIdAsync(Guid caseId, Guid assetId, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .Include(a => a.SecuredCreditorParty)
            .FirstOrDefaultAsync(a => a.Id == assetId && a.CaseId == caseId, ct)
            ?? throw new NotFoundException("Asset", assetId);
        return asset.ToDto();
    }

    public async Task<AssetDto> CreateAsync(Guid caseId, CreateAssetRequest request, CancellationToken ct = default)
    {
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            AssetType = request.AssetType,
            Description = request.Description,
            EstimatedValue = request.EstimatedValue,
            EncumbranceDetails = request.EncumbranceDetails,
            SecuredCreditorPartyId = request.SecuredCreditorPartyId,
            Status = request.Status ?? "Identified",
            Notes = request.Notes,
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(asset).Reference(a => a.SecuredCreditorParty).LoadAsync(ct);
        await _audit.LogEntityAsync("Asset Added", "Asset", asset.Id,
            newValues: new { caseId, request.AssetType, request.Description, request.EstimatedValue });

        return asset.ToDto();
    }

    public async Task<AssetDto> UpdateAsync(Guid caseId, Guid assetId, UpdateAssetRequest request, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .Include(a => a.SecuredCreditorParty)
            .FirstOrDefaultAsync(a => a.Id == assetId && a.CaseId == caseId, ct)
            ?? throw new NotFoundException("Asset", assetId);

        var old = new { asset.AssetType, asset.Description, asset.EstimatedValue, asset.Status, asset.SaleProceeds };

        if (request.AssetType != null) asset.AssetType = request.AssetType;
        if (request.Description != null) asset.Description = request.Description;
        if (request.EstimatedValue.HasValue) asset.EstimatedValue = request.EstimatedValue;
        if (request.EncumbranceDetails != null) asset.EncumbranceDetails = request.EncumbranceDetails;
        if (request.SecuredCreditorPartyId.HasValue) asset.SecuredCreditorPartyId = request.SecuredCreditorPartyId;
        if (request.Status != null) asset.Status = request.Status;
        if (request.SaleProceeds.HasValue) asset.SaleProceeds = request.SaleProceeds;
        if (request.DisposedAt.HasValue) asset.DisposedAt = request.DisposedAt;
        if (request.Notes != null) asset.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Asset Updated", "Asset", asset.Id,
            old, new { asset.AssetType, asset.Description, asset.EstimatedValue, asset.Status, asset.SaleProceeds });

        return asset.ToDto();
    }

    public async Task DeleteAsync(Guid caseId, Guid assetId, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.CaseId == caseId, ct)
            ?? throw new NotFoundException("Asset", assetId);

        await _audit.LogEntityAsync("Asset Removed", "Asset", assetId,
            oldValues: new { caseId, asset.AssetType, asset.Description, asset.EstimatedValue },
            severity: "Warning");
        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync(ct);
    }
}
