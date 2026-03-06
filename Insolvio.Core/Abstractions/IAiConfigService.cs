using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Manages the system-level AI provider configuration.
/// </summary>
public interface IAiConfigService
{
    /// <summary>Returns current AI config (API key never exposed).</summary>
    Task<AiConfigDto> GetAsync(CancellationToken ct = default);

    /// <summary>Creates or updates the AI configuration.</summary>
    Task<AiConfigDto> UpdateAsync(UpdateAiConfigRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the decrypted API key for internal use by AI service calls.
    /// Returns null if no key is configured or decryption fails.
    /// </summary>
    Task<string?> GetDecryptedApiKeyAsync(CancellationToken ct = default);
}
