using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

public interface IAssetService
{
    Task<List<AssetDto>> GetAllAsync(Guid caseId, CancellationToken ct = default);
    Task<AssetDto> GetByIdAsync(Guid caseId, Guid assetId, CancellationToken ct = default);
    Task<AssetDto> CreateAsync(Guid caseId, CreateAssetRequest request, CancellationToken ct = default);
    Task<AssetDto> UpdateAsync(Guid caseId, Guid assetId, UpdateAssetRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid caseId, Guid assetId, CancellationToken ct = default);
}
