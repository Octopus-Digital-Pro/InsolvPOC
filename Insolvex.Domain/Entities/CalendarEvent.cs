namespace Insolvex.Domain.Entities;

/// <summary>
/// A calendar event linked to a case (hearings, meetings, deadlines, scheduled emails).
/// Per InsolvencyAppRules: used for creditor meetings, filing deadlines, and task reminders.
/// </summary>
public class CalendarEvent : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public bool AllDay { get; set; }

    /// <summary>Physical location or online meeting link.</summary>
    public string? Location { get; set; }

    /// <summary>Event type: Hearing, Meeting, Deadline, ScheduledEmail, Reminder, Filing.</summary>
    public string EventType { get; set; } = "Deadline";

    /// <summary>JSON array of participant info [{name, email, role}].</summary>
    public string? ParticipantsJson { get; set; }

    /// <summary>ICS calendar file URL for external calendar integration.</summary>
    public string? IcsUrl { get; set; }

    /// <summary>Whether the event has been synced to an external calendar.</summary>
    public bool SyncedExternal { get; set; }

    /// <summary>Related task ID (if this event was created from a task deadline).</summary>
    public Guid? RelatedTaskId { get; set; }

    /// <summary>Related creditor meeting ID (for meeting events).</summary>
    public Guid? RelatedMeetingId { get; set; }

    /// <summary>Whether the event is cancelled.</summary>
    public bool IsCancelled { get; set; }
}
