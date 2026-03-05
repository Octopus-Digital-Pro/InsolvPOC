using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Manages per-tenant AI feature configuration.
/// </summary>
public interface ITenantAiConfigService
{
    /// <summary>Returns the AI config for the current user's tenant (or a given tenant).</summary>
    Task<TenantAiConfigDto> GetAsync(Guid? tenantId = null, CancellationToken ct = default);

    /// <summary>Creates or updates the AI config for a tenant (GlobalAdmin only).</summary>
    Task<TenantAiConfigDto> UpdateAsync(Guid tenantId, UpdateTenantAiConfigRequest request, CancellationToken ct = default);

    /// <summary>Updates only the API key / model for the current user's own tenant (TenantAdmin self-service).</summary>
    Task<TenantAiConfigDto> UpdateOwnApiKeyAsync(UpdateTenantAiKeyRequest request, CancellationToken ct = default);

    /// <summary>Returns the decrypted tenant API key, or null if not set.</summary>
    Task<string?> GetDecryptedApiKeyAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether AI features are enabled for the current user's tenant.
    /// Used to gate AI features without loading full config.
    /// </summary>
    Task<bool> IsAiEnabledAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Increments the monthly token usage counter and returns whether the limit
    /// has been reached (true = over limit, caller should refuse the AI call).
    /// </summary>
    Task<bool> RecordTokenUsageAsync(Guid tenantId, int tokensUsed, CancellationToken ct = default);
}
