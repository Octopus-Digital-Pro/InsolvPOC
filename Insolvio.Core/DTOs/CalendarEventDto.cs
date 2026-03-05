namespace Insolvio.Core.DTOs;

public record CalendarEventDto(
    Guid Id,
    Guid CaseId,
    string Title,
    string? Description,
    DateTime Start,
    DateTime? End,
    bool AllDay,
    string? Location,
  string EventType,
    string? ParticipantsJson,
    string? IcsUrl,
  bool SyncedExternal,
    Guid? RelatedTaskId,
    Guid? RelatedMeetingId,
    bool IsCancelled,
  DateTime CreatedOn
);

public record CreateCalendarEventRequest(
    Guid CaseId,
    string Title,
  string? Description,
    DateTime Start,
    DateTime? End,
    bool AllDay,
    string? Location,
    string EventType,
    string? ParticipantsJson,
    Guid? RelatedTaskId
);

public record UpdateCalendarEventRequest(
    string? Title,
    string? Description,
    DateTime? Start,
    DateTime? End,
    string? Location,
  bool? IsCancelled
);
