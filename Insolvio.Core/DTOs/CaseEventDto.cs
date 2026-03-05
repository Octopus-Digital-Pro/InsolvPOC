namespace Insolvio.Core.DTOs;

/// <summary>
/// Represents a single entry on the case timeline.
/// </summary>
public record CaseEventDto(
    Guid Id,
    Guid CaseId,
    string Category,
    string EventType,
    string Description,
    DateTime OccurredAt,
    Guid? ActorUserId,
    string? ActorName,
    string? LinkedEntityType,
    Guid? LinkedEntityId,
    string? InvolvedPartiesJson,
    string? KeyDatesJson,
    string? DocumentSummary,
    string? ExtractedActionsJson,
    string Severity,
    string? MetadataJson
);

/// <summary>
/// Paginated page of case timeline events.
/// </summary>
public record CaseEventsPageDto(
    List<CaseEventDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// Payload for recording any system or user event on a case timeline.
/// </summary>
public class RecordCaseEventRequest
{
    /// <summary>Category: Document | Task | Phase | Party | Calendar | Deadline | Communication | Signing | AI | System</summary>
    public string Category { get; set; } = "System";

    /// <summary>e.g. "Document.Uploaded", "Task.Completed", "Phase.Advanced", "Deadline.Missed"</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Human-readable description for display in the timeline.</summary>
    public string Description { get; set; } = string.Empty;

    public DateTime? OccurredAt { get; set; }

    public string? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }

    /// <summary>Party objects: [{ "id": "...", "name": "...", "role": "..." }]</summary>
    public object? InvolvedParties { get; set; }

    /// <summary>Important dates: [{ "date": "...", "meaning": "..." }]</summary>
    public object? KeyDates { get; set; }

    public string? DocumentSummary { get; set; }
    public object? ExtractedActions { get; set; }

    public string Severity { get; set; } = "Info";

    /// <summary>Arbitrary structured payload for AI ingestion.</summary>
    public object? Metadata { get; set; }
}
