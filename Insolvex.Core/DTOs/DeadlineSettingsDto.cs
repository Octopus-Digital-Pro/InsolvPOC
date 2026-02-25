namespace Insolvex.Core.DTOs;

public record TenantDeadlineSettingsDto(
    Guid Id,
    int SendInitialNoticeWithinDays,
    int ClaimDeadlineDaysFromNotice,
    int ObjectionDeadlineDaysFromNotice,
    int MeetingNoticeMinimumDays,
    int ReportEveryNDays,
    bool UseBusinessDays,
    bool AdjustToNextWorkingDay,
    string ReminderDaysBeforeDeadline,
    string? EmailFromName,
    int UrgentQueueHoursBeforeDeadline,
    bool AutoAssignBackupOnCriticalOverdue
);

public record CaseDeadlineOverrideDto(
    Guid Id,
    Guid CaseId,
    string DeadlineKey,
    string? OriginalValue,
    string OverrideValue,
    string Reason,
    Guid? OverriddenByUserId,
    DateTime OverriddenAt,
    bool IsActive
);

public record UpdateTenantDeadlineSettingsRequest(
    int? SendInitialNoticeWithinDays = null,
    int? ClaimDeadlineDaysFromNotice = null,
    int? ObjectionDeadlineDaysFromNotice = null,
    int? MeetingNoticeMinimumDays = null,
    int? ReportEveryNDays = null,
    bool? UseBusinessDays = null,
    bool? AdjustToNextWorkingDay = null,
    string? ReminderDaysBeforeDeadline = null,
    int? UrgentQueueHoursBeforeDeadline = null,
    bool? AutoAssignBackupOnCriticalOverdue = null,
    string? EmailFromName = null
);

public record CreateCaseDeadlineOverrideRequest(
    string DeadlineKey,
    string OverrideValue,
    string Reason
);

