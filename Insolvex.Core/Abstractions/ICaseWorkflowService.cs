using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Manages per-case workflow stages: initialization, validation, gating, and progression.
/// </summary>
public interface ICaseWorkflowService
{
    /// <summary>
    /// Get all workflow stages for a case. If stages haven't been initialized yet,
    /// they are created from the resolved stage definitions.
    /// </summary>
    Task<List<CaseWorkflowStageDto>> GetStagesAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Validate a stage's requirements against the current case data.
    /// Returns what's satisfied and what's missing.
    /// </summary>
    Task<ValidationResultDto> ValidateStageAsync(Guid caseId, string stageKey, CancellationToken ct = default);

    /// <summary>
    /// Advance a stage to InProgress. Validates that the previous required stages are done.
    /// </summary>
    Task<CaseWorkflowStageDto> StartStageAsync(Guid caseId, string stageKey, CancellationToken ct = default);

    /// <summary>
    /// Complete a stage. Validates requirements before allowing completion.
    /// </summary>
    Task<CaseWorkflowStageDto> CompleteStageAsync(Guid caseId, string stageKey, CancellationToken ct = default);

    /// <summary>
    /// Skip a stage (e.g. not applicable for this procedure type).
    /// </summary>
    Task<CaseWorkflowStageDto> SkipStageAsync(Guid caseId, string stageKey, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Reopen a completed or skipped stage back to InProgress.
    /// </summary>
    Task<CaseWorkflowStageDto> ReopenStageAsync(Guid caseId, string stageKey, CancellationToken ct = default);

    /// <summary>
    /// Returns whether all stages are Completed or Skipped, and which ones are still pending.
    /// </summary>
    Task<CaseCloseabilityDto> GetCloseabilityAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Close the case. All stages must be Completed or Skipped unless overridePendingStages=true.
    /// When overriding, explanation is mandatory (min 20 chars). An audit log is written.
    /// </summary>
    Task CloseCaseAsync(Guid caseId, string? explanation, bool overridePendingStages, CancellationToken ct = default);

    /// <summary>
    /// Override the deadline date for a workflow stage. Tenant admin only.
    /// Requires a mandatory note. Creates an audit log entry.
    /// </summary>
    Task<CaseWorkflowStageDto> SetStageDeadlineAsync(Guid caseId, string stageKey, DateTime newDate, string note, CancellationToken ct = default);
}
