namespace Insolvex.Domain.Entities;

/// <summary>
/// Tracks the progress of a single workflow stage for a specific insolvency case.
/// One row per (CaseId, StageKey) combination.
/// </summary>
public class CaseWorkflowStage : BaseEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase Case { get; set; } = null!;

    /// <summary>Foreign key to the resolved WorkflowStageDefinition.</summary>
    public Guid StageDefinitionId { get; set; }
    public virtual WorkflowStageDefinition StageDefinition { get; set; } = null!;

    /// <summary>Cached stage key for quick lookups without joining.</summary>
    public string StageKey { get; set; } = string.Empty;

    /// <summary>Display sort order (copied from definition at initialization time).</summary>
    public int SortOrder { get; set; }

    /// <summary>Current status of this stage for this case.</summary>
    public CaseWorkflowStatus Status { get; set; } = CaseWorkflowStatus.NotStarted;

    /// <summary>When this stage was started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When this stage was completed or skipped.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>User who completed or skipped this stage.</summary>
    public string? CompletedBy { get; set; }

    /// <summary>JSON: validation results from the last check (missing fields, missing roles, etc.).</summary>
    public string? ValidationResultJson { get; set; }

    /// <summary>Optional notes, e.g. reason for skipping.</summary>
    public string? Notes { get; set; }
}

public enum CaseWorkflowStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3
}
