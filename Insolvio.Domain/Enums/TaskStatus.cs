namespace Insolvio.Domain.Enums;

/// <summary>
/// Task status aligned with InsolvencyAppRules:
/// Open, InProgress, Blocked, Done, Overdue, Cancelled.
/// </summary>
public enum TaskStatus
{
    Open,
    InProgress,
    Blocked,
    Done,
    Overdue,
    Cancelled
}
