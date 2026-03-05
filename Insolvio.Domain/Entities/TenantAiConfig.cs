namespace Insolvio.Domain.Entities;

/// <summary>
/// Per-tenant AI feature configuration.
/// GlobalAdmin and TenantAdmin can view; only GlobalAdmin can update the
/// system-level key, but TenantAdmin can toggle features for their own tenant.
/// </summary>
public class TenantAiConfig : TenantScopedEntity
{
    /// <summary>When false, the AI tab is hidden on all cases for this tenant
    /// and all AI calls (summary, chat) are suppressed.</summary>
    public bool AiEnabled { get; set; } = false;

    /// <summary>Maximum number of AI tokens (input + output) that may be
    /// consumed by this tenant per calendar month. 0 = unlimited.</summary>
    public int MonthlyTokenLimit { get; set; } = 100_000;

    /// <summary>Tokens consumed in the current calendar month.</summary>
    public int CurrentMonthTokensUsed { get; set; } = 0;

    /// <summary>Year+month stamp of the last usage reset (format: YYYYMM).</summary>
    public int UsageResetMonth { get; set; } = 0;

    /// <summary>Enable AI-powered case summary generation.</summary>
    public bool SummaryEnabled { get; set; } = true;

    /// <summary>Enable AI chat assistant on cases.</summary>
    public bool ChatEnabled { get; set; } = true;

    /// <summary>Number of days of activity to include in summary context (default 30).</summary>
    public int SummaryActivityDays { get; set; } = 30;

    /// <summary>Optional admin notes for this tenant's AI config.</summary>
    public string? Notes { get; set; }

    // ── Tenant-owned AI key override ───────────────────────────────────────────

    /// <summary>If set, overrides the system-level AI provider for this tenant.</summary>
    public string? Provider { get; set; }

    /// <summary>AES-256 encrypted tenant API key. Null = use system key.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>Optional custom API endpoint (e.g. Azure OpenAI deployment URL).</summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>Model name override (e.g. "gpt-4o", "claude-3-5-sonnet-20241022").</summary>
    public string? ModelName { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
