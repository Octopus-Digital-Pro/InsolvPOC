using Insolvex.Domain.Enums;

namespace Insolvex.Domain.Entities;

/// <summary>
/// Tracks the progress of a specific workflow phase within an insolvency case.
/// Each case has one CasePhase record per applicable PhaseType.
/// </summary>
public class CasePhase : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

 public PhaseType PhaseType { get; set; }
    public PhaseStatus Status { get; set; } = PhaseStatus.NotStarted;

    /// <summary>Order in the workflow (for display sorting)</summary>
    public int SortOrder { get; set; }

    /// <summary>Date this phase was started</summary>
    public DateTime? StartedOn { get; set; }

    /// <summary>Date this phase was completed</summary>
    public DateTime? CompletedOn { get; set; }

    /// <summary>Legal deadline for this phase (if any)</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Internal notes / observations about this phase</summary>
    public string? Notes { get; set; }

    /// <summary>Reference to the court decision / BPI publication number</summary>
    public string? CourtDecisionRef { get; set; }

    /// <summary>Id of user who last updated this phase</summary>
    public Guid? UpdatedByUserId { get; set; }
}
