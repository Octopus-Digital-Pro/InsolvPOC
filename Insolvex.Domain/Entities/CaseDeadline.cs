namespace Insolvex.Domain.Entities;

/// <summary>
/// A custom or phase-linked deadline for an insolvency case.
/// These are managed at case level and may be relative to case creation date,
/// a phase start date, or set manually.
/// </summary>
public class CaseDeadline : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase Case { get; set; } = null!;

    /// <summary>Display label for this deadline.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The actual due date (computed or manually set).</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Whether this deadline has been completed/acknowledged.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// If linked to a workflow phase, the StageKey of that phase.
    /// Null for standalone case deadlines.
    /// </summary>
    public string? PhaseKey { get; set; }

    /// <summary>
    /// Origin of this deadline: "manual" | "caseCreation" | "phaseStart".
    /// Used for display and to know how to recompute on template change.
    /// </summary>
    public string RelativeTo { get; set; } = "manual";

    /// <summary>Number of days offset from the RelativeTo anchor (used for display/edit).</summary>
    public int? OffsetDays { get; set; }

    /// <summary>Optional notes about this deadline.</summary>
    public string? Notes { get; set; }
}
