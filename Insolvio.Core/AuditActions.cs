namespace Insolvio.Core;

/// <summary>
/// Typed constants for audit log action strings.
/// Used in AuditLog.Action column for consistent filtering and reporting.
/// </summary>
public static class AuditActions
{
    // Case
    public const string CaseFieldEdited = "case.field_edited";
    public const string ProcedureTypeChanged = "case.procedure_type_changed";

    // Workflow
    public const string WorkflowTransition = "workflow.transition";
    public const string StageStarted = "workflow.stage_started";
    public const string StageClosed = "workflow.stage_closed";

    // Documents
    public const string DocumentGenerated = "document.generated";
    public const string DocumentSavedToCase = "document.saved_to_case";

    // Tasks
    public const string TaskCreated = "task.created";
    public const string TaskCompleted = "task.completed";
    public const string TaskAssigneeAdded = "task.assignee_added";
    public const string TaskAssigneeRemoved = "task.assignee_removed";
    public const string TaskUpdated = "task.updated";
    public const string TaskDeleted = "task.deleted";

    // Reports
    public const string ReportGenerated = "report.generated";
}
