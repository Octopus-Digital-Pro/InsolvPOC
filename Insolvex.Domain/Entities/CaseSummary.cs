namespace Insolvex.Domain.Entities;

/// <summary>
/// AI-generated case status summary. Stored and displayed in the Overview tab.
/// Regenerated after document uploads, task changes, stage transitions, and daily refresh.
/// </summary>
public class CaseSummary : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  /// <summary>The AI model/provider used to generate this summary.</summary>
  public string? Model { get; set; }

  /// <summary>JSON snapshot of the case data at time of generation (for audit/reproducibility).</summary>
  public string? SnapshotJson { get; set; }

  /// <summary>The human-readable summary text (markdown).</summary>
  public string Text { get; set; } = string.Empty;

  /// <summary>
  /// JSON object with summaries by system language, e.g.
  /// {"en":"...","ro":"...","hu":"..."}.
  /// </summary>
  public string? TextByLanguageJson { get; set; }

  /// <summary>Structured "what must happen next" (JSON array of top tasks).</summary>
  public string? NextActionsJson { get; set; }

  /// <summary>Structured "risks" (JSON array of risk items).</summary>
  public string? RisksJson { get; set; }

  /// <summary>Structured "upcoming deadlines" (JSON array).</summary>
  public string? UpcomingDeadlinesJson { get; set; }

  /// <summary>What triggered this summary generation.</summary>
  public string? Trigger { get; set; }

  /// <summary>Generation timestamp.</summary>
  public DateTime GeneratedAt { get; set; }
}
