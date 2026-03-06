namespace Insolvio.Domain.Entities;

/// <summary>
/// System-level AI provider configuration. Not tenant-scoped — shared globally.
/// Stored as a singleton record (only one row). The API key is AES-256 encrypted.
/// Only GlobalAdmin can view or modify this configuration.
/// </summary>
public class AiSystemConfig : BaseEntity
{
    /// <summary>AI provider name: OpenAI | AzureOpenAI | Anthropic | Google | Custom</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>AES-256 encrypted API key. Null if not yet configured.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>API base URL. Required for Azure OpenAI and custom self-hosted models.</summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>Model identifier — e.g. "gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet-20241022".</summary>
    public string? ModelName { get; set; }

    /// <summary>Azure OpenAI deployment name (only required for AzureOpenAI provider).</summary>
    public string? DeploymentName { get; set; }

    /// <summary>When true, AI features are actively used. When false, all AI calls fall back to stub/heuristic.</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>Optional admin notes — e.g. which model version is in use, cost alerts, etc.</summary>
    public string? Notes { get; set; }
}
