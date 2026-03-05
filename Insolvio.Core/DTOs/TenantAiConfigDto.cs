namespace Insolvio.Core.DTOs;

public record TenantAiConfigDto(
    Guid Id,
    bool AiEnabled,
    int MonthlyTokenLimit,
    int CurrentMonthTokensUsed,
    bool SummaryEnabled,
    bool ChatEnabled,
    int SummaryActivityDays,
    string? Notes,
    DateTime? UpdatedAt,
    // Per-tenant AI key override
    bool HasApiKey,
    string? Provider,
    string? ApiEndpoint,
    string? ModelName
);

public class UpdateTenantAiConfigRequest
{
    public bool AiEnabled { get; set; }
    public int MonthlyTokenLimit { get; set; } = 100_000;
    public bool SummaryEnabled { get; set; } = true;
    public bool ChatEnabled { get; set; } = true;
    public int SummaryActivityDays { get; set; } = 30;
    public string? Notes { get; set; }
    // Key override (GlobalAdmin can set per-tenant keys too)
    public string? ApiKey { get; set; }
    public string? Provider { get; set; }
    public string? ApiEndpoint { get; set; }
    public string? ModelName { get; set; }
}

/// <summary>Request for TenantAdmin to manage only their own AI key / model.</summary>
public class UpdateTenantAiKeyRequest
{
    /// <summary>Provider: OpenAI | AzureOpenAI | Anthropic | Google | Custom</summary>
    public string? Provider { get; set; }
    /// <summary>null = keep existing, "" = clear, any other value = new key</summary>
    public string? ApiKey { get; set; }
    public string? ApiEndpoint { get; set; }
    public string? ModelName { get; set; }
}

public record AiChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    int TokensUsed,
    string? Model,
    DateTime CreatedAt,
    Guid? UserId,
    string? UserName
);

public class AiChatRequest
{
    /// <summary>The user's message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Preferred response language: "en" | "ro" | "hu".</summary>
    public string Language { get; set; } = "ro";
}

public record AiChatResponse(
    AiChatMessageDto UserMessage,
    AiChatMessageDto AssistantMessage,
    int TokensUsed
);

public class AiSummaryRequest
{
    /// <summary>Preferred language for the returned summary: "en" | "ro" | "hu".</summary>
    public string Language { get; set; } = "ro";
}
