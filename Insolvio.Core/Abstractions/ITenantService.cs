using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for tenant (organisation) management.
/// All mutations are audited.
/// </summary>
public interface ITenantService
{
    Task<List<TenantSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<TenantDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
