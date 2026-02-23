namespace Insolvex.Domain.Enums;

/// <summary>
/// Status of a case workflow phase.
/// </summary>
public enum PhaseStatus
{
    /// <summary>Not yet started</summary>
    NotStarted,

    /// <summary>Currently active</summary>
    InProgress,

    /// <summary>Completed successfully</summary>
    Completed,

    /// <summary>Skipped (not applicable for this procedure type)</summary>
    Skipped,

    /// <summary>Blocked / waiting for external action</summary>
    Blocked,
}
