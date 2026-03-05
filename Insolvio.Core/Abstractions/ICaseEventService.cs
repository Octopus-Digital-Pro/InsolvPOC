using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Records and queries the immutable case event timeline.
/// Every document upload, task change, phase transition, party action,
/// and deadline event produces a CaseEvent entry.
/// These events are the primary data source for the AI case-status summariser.
/// </summary>
public interface ICaseEventService
{
    // ── Recording ──────────────────────────────────────────────────────────

    /// <summary>Record a raw event entry.</summary>
    Task<CaseEventDto> RecordAsync(Guid caseId, RecordCaseEventRequest request, CancellationToken ct = default);

    /// <summary>Convenience: record a document-upload event with AI summary.</summary>
    Task<CaseEventDto> RecordDocumentUploadedAsync(Guid caseId, Guid documentId,
        string fileName, string docType, string? aiSummary,
        object? extractedParties, object? extractedDates, object? extractedActions,
        CancellationToken ct = default);

    /// <summary>Convenience: record a task status-change event.</summary>
    Task<CaseEventDto> RecordTaskEventAsync(Guid caseId, Guid taskId,
        string eventType, string taskTitle, string? description,
        object? involvedParties, DateTime? deadline,
        CancellationToken ct = default);

    /// <summary>Convenience: record a missed-deadline event.</summary>
    Task<CaseEventDto> RecordDeadlineEventAsync(Guid caseId,
        string deadlineName, DateTime deadlineDate, bool isMissed,
        Guid? linkedEntityId, string? linkedEntityType,
        object? involvedParties, CancellationToken ct = default);

    // ── Querying ───────────────────────────────────────────────────────────

    /// <summary>Get a paginated page of events for a case, newest first.</summary>
    Task<CaseEventsPageDto> GetByCaseAsync(Guid caseId, int page = 1, int pageSize = 50,
        string? category = null, CancellationToken ct = default);

    /// <summary>
    /// Build a structured snapshot of case events for AI prompt injection.
    /// Returns a condensed, token-efficient list of events with parties and dates.
    /// </summary>
    Task<string> BuildAiContextAsync(Guid caseId, int maxEvents = 100, CancellationToken ct = default);
}
