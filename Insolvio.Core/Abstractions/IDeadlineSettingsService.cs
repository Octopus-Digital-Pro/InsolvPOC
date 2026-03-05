using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for tenant-level and case-level deadline settings.
/// All mutations are audited.
/// </summary>
public interface IDeadlineSettingsService
{
    Task<TenantDeadlineSettingsDto?> GetTenantSettingsAsync(CancellationToken ct = default);
    Task<TenantDeadlineSettingsDto> UpsertTenantSettingsAsync(
        UpdateTenantDeadlineSettingsRequest request, CancellationToken ct = default);
    Task<List<CaseDeadlineOverrideDto>> GetCaseOverridesAsync(Guid caseId, CancellationToken ct = default);
    Task<CaseDeadlineOverrideDto> CreateCaseOverrideAsync(
        Guid caseId, CreateCaseDeadlineOverrideRequest request, CancellationToken ct = default);
    Task DeactivateCaseOverrideAsync(Guid caseId, Guid overrideId, CancellationToken ct = default);
}
