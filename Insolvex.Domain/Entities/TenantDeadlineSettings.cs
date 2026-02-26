namespace Insolvex.Domain.Entities;

/// <summary>
/// Per-tenant deadline configuration settings.
/// Replaces scattered SystemConfig keys with a structured, tenant-scoped entity.
/// Per InsolvencyAppRules section 2: Company Settings (per tenant) with default deadline periods.
/// Resolution hierarchy: case override ? case-type default ? tenant settings ? global defaults.
/// </summary>
public class TenantDeadlineSettings : TenantScopedEntity
{
// ?? Deadline periods (days from NoticeDate) ??

    /// <summary>Days from NoticeDate to send initial notice (default: 2).</summary>
    public int SendInitialNoticeWithinDays { get; set; } = 2;

    /// <summary>Days from NoticeDate for claim submission deadline (default: 30).</summary>
    public int ClaimDeadlineDaysFromNotice { get; set; } = 30;

    /// <summary>Days from NoticeDate for objection deadline (default: 45).</summary>
 public int ObjectionDeadlineDaysFromNotice { get; set; } = 45;

    /// <summary>Minimum days before a meeting to send notices (default: 14).</summary>
    public int MeetingNoticeMinimumDays { get; set; } = 14;

    /// <summary>Interval in days for periodic reports (default: 30).</summary>
    public int ReportEveryNDays { get; set; } = 30;

    // ?? Computation rules ??

    /// <summary>If true, count business days instead of calendar days.</summary>
    public bool UseBusinessDays { get; set; }

    /// <summary>If true, push deadlines falling on weekend/holiday to next working day.</summary>
    public bool AdjustToNextWorkingDay { get; set; } = true;

    /// <summary>Comma-separated reminder days before deadline (e.g. "7,3,1,0").</summary>
    public string ReminderDaysBeforeDeadline { get; set; } = "7,3,1,0";

    // ?? Email settings ??

    /// <summary>Sending domain for outbound emails.</summary>
    public string? EmailSendingDomain { get; set; }

    /// <summary>Default email signature block (HTML).</summary>
public string? EmailSignatureHtml { get; set; }

    /// <summary>Default "From" name for emails.</summary>
 public string? EmailFromName { get; set; }

    // ?? Escalation ??

    /// <summary>Hours before deadline to trigger urgent queue (default: 24).</summary>
  public int UrgentQueueHoursBeforeDeadline { get; set; } = 24;

/// <summary>Whether to auto-assign a backup user on critical overdue tasks.</summary>
    public bool AutoAssignBackupOnCriticalOverdue { get; set; }
}
