using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Interface for AI case status summary generation and persistence.
/// </summary>
public interface ICaseSummaryService
{
  /// <summary>Generate a summary using AI without persisting it.</summary>
  Task<CaseSummaryResult> GenerateAsync(Guid caseId);

  /// <summary>Generate a summary and persist it to the database.</summary>
  Task<CaseSummaryDto> GenerateAndSaveAsync(Guid caseId, string? trigger = null);

  /// <summary>Get the latest saved summary for a case.</summary>
  Task<CaseSummaryDto?> GetLatestAsync(Guid caseId);

  /// <summary>Get the summary history (most recent first).</summary>
  Task<List<CaseSummaryHistoryItem>> GetHistoryAsync(Guid caseId, int take = 10);
}

public class CaseSummaryResult
{
  public string Text { get; set; } = string.Empty;
  public Dictionary<string, string> TextByLanguage { get; set; } = new();
  public List<string> NextActions { get; set; } = new();
  public List<string> Risks { get; set; } = new();
  public List<CaseSummaryDeadline> UpcomingDeadlines { get; set; } = new();
  public string? SnapshotJson { get; set; }
  public string? Error { get; set; }
}

public class CaseSummaryDeadline
{
  public string Title { get; set; } = string.Empty;
  public DateTime Deadline { get; set; }
  public bool IsOverdue { get; set; }
}
