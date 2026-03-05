namespace Insolvio.Core.DTOs;

/// <summary>
/// AI system configuration — the API key is NEVER returned; only <see cref="HasApiKey"/> indicates presence.
/// </summary>
public record AiConfigDto(
    Guid Id,
    string Provider,
    bool HasApiKey,
    string? ApiEndpoint,
    string? ModelName,
    string? DeploymentName,
    bool IsEnabled,
    string? Notes,
    DateTime? UpdatedAt
);

public class UpdateAiConfigRequest
{
    /// <summary>Provider name: OpenAI | AzureOpenAI | Anthropic | Google | Custom</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// New API key to encrypt and store.
    /// Null = leave existing key unchanged.
    /// Empty string = clear the stored key.
    /// Any other value = encrypt and replace.
    /// </summary>
    public string? ApiKey { get; set; }

    public string? ApiEndpoint { get; set; }
    public string? ModelName { get; set; }
    public string? DeploymentName { get; set; }
    public bool IsEnabled { get; set; }
    public string? Notes { get; set; }
}
