namespace Insolvex.Domain.Entities;

/// <summary>
/// A single message in the AI chat session for a specific case.
/// Role: "user" | "assistant" | "system"
/// </summary>
public class AiChatMessage : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

    /// <summary>"user" | "assistant"</summary>
    public string Role { get; set; } = "user";

    /// <summary>The message text (may be markdown for assistant messages).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Number of tokens consumed (input + output) for this exchange.</summary>
    public int TokensUsed { get; set; } = 0;

    /// <summary>AI model used to generate the response.</summary>
    public string? Model { get; set; }

    /// <summary>Wall-clock timestamp of when the message was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>ID of the user who sent this message (null for assistant messages).</summary>
    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }
}
