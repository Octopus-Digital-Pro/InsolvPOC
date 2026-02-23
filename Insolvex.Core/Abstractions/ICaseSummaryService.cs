namespace Insolvex.Core.Abstractions;

/// <summary>
/// Interface for AI case status summary generation.
/// </summary>
public interface ICaseSummaryService
{
    Task<CaseSummaryResult> GenerateAsync(Guid caseId);
}

public class CaseSummaryResult
{
 public string Text { get; set; } = string.Empty;
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
