using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// AI-powered case summary and chat assistant service.
/// Generates rich, context-aware summaries and handles conversational interaction.
/// </summary>
public interface ICaseAiService
{
    /// <summary>
    /// Generate an AI-powered case summary using the last N days of activity.
    /// Includes debtor info, assets, creditors, tasks, events, and documents.
    /// Returns summary in the specified language (en/ro/hu).
    /// Falls back to stub summary when AI is unavailable.
    /// </summary>
    Task<CaseSummaryDto> GenerateSummaryAsync(Guid caseId, string language = "ro", CancellationToken ct = default);

    /// <summary>
    /// Get the latest saved summary for a case. Returns null if none has been generated yet.
    /// </summary>
    Task<CaseSummaryDto?> GetLatestSummaryAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Send a user message to the AI assistant and get a response.
    /// Full case context is included as system prompt. History is persisted.
    /// </summary>
    Task<AiChatResponse> ChatAsync(Guid caseId, AiChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieve the chat history for a case (most recent first).
    /// </summary>
    Task<List<AiChatMessageDto>> GetChatHistoryAsync(Guid caseId, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Clear all chat history for a case.
    /// </summary>
    Task ClearChatHistoryAsync(Guid caseId, CancellationToken ct = default);
}
