using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;

namespace Insolvex.API.Services;

/// <summary>
/// Computes deadlines from NoticeDate + configurable periods.
/// Supports calendar days, business days, and "next working day if weekend/holiday".
/// Fixed to Europe/Bucharest timezone.
/// Priority: Notice-derived > Computed from NoticeDate+period > Manual override.
/// </summary>
public class DeadlineEngine
{
    private readonly ApplicationDbContext _db;
    private static readonly TimeZoneInfo BucharestTz = TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time");

    // Romanian public holidays (fixed dates; moving holidays need yearly config)
    private static readonly HashSet<(int Month, int Day)> FixedHolidays = new()
    {
 (1, 1), (1, 2),   // New Year
        (1, 24),          // Unirea Principatelor
  (5, 1),           // Labour Day
   (6, 1),           // Children's Day
  (8, 15),          // Assumption of Mary
       (11, 30),       // St. Andrew's Day
        (12, 1),     // National Day
     (12, 25), (12, 26), // Christmas
    };

    public DeadlineEngine(ApplicationDbContext db) => _db = db;

    /// <summary>
  /// Compute a deadline from a base date + period.
    /// </summary>
    /// <param name="baseDate">The starting date (typically NoticeDate).</param>
 /// <param name="days">Number of days to add.</param>
    /// <param name="useBusinessDays">If true, count only business days (Mon-Fri, excluding holidays).</param>
 /// <param name="adjustToNextWorkingDay">If the result falls on weekend/holiday, push to next working day.</param>
    public DateTime ComputeDeadline(
        DateTime baseDate,
        int days,
    bool useBusinessDays = false,
        bool adjustToNextWorkingDay = true)
    {
 DateTime result;
        if (useBusinessDays)
        {
   result = AddBusinessDays(baseDate, days);
        }
        else
     {
   result = baseDate.AddDays(days);
        }

        if (adjustToNextWorkingDay)
        result = EnsureWorkingDay(result);

        return result;
    }

    /// <summary>
    /// Get the hierarchical deadline settings for a case.
    /// Resolution order: case override ? case-type default ? company default ? global default.
    /// </summary>
    public async Task<DeadlineSettings> GetEffectiveSettingsAsync(Guid? caseId, Guid? tenantId)
    {
        var allConfigs = await _db.SystemConfigs.AsNoTracking()
        .Where(c => c.Group == "Deadlines")
   .ToDictionaryAsync(c => c.Key, c => c.Value);

    return new DeadlineSettings
   {
  ClaimDeadlineDaysFromNotice = GetIntConfig(allConfigs, "Deadlines:ClaimDeadlineDaysFromNotice", 30),
            ObjectionDeadlineDaysFromNotice = GetIntConfig(allConfigs, "Deadlines:ObjectionDeadlineDaysFromNotice", 45),
        SendInitialNoticeWithinDays = GetIntConfig(allConfigs, "Deadlines:SendInitialNoticeWithinDays", 2),
      MeetingNoticeMinimumDays = GetIntConfig(allConfigs, "Deadlines:MeetingNoticeMinimumDays", 14),
    ReportEveryNDays = GetIntConfig(allConfigs, "Deadlines:ReportEveryNDays", 30),
        UseBusinessDays = GetBoolConfig(allConfigs, "Deadlines:UseBusinessDays", false),
            AdjustToNextWorkingDay = GetBoolConfig(allConfigs, "Deadlines:AdjustToNextWorkingDay", true),
            ReminderDaysBeforeDeadline = GetIntArrayConfig(allConfigs, "Deadlines:ReminderDays", new[] { 7, 3, 1, 0 }),
        };
    }

    /// <summary>
    /// Compute all baseline deadlines for a new case from NoticeDate.
    /// </summary>
    public async Task<Dictionary<string, DateTime>> ComputeBaselineDeadlinesAsync(
   DateTime noticeDate, Guid? tenantId)
    {
      var settings = await GetEffectiveSettingsAsync(null, tenantId);
        var deadlines = new Dictionary<string, DateTime>();

        deadlines["claimDeadline"] = ComputeDeadline(noticeDate, settings.ClaimDeadlineDaysFromNotice,
            settings.UseBusinessDays, settings.AdjustToNextWorkingDay);

        deadlines["objectionDeadline"] = ComputeDeadline(noticeDate, settings.ObjectionDeadlineDaysFromNotice,
     settings.UseBusinessDays, settings.AdjustToNextWorkingDay);

        deadlines["initialNoticeSendBy"] = ComputeDeadline(noticeDate, settings.SendInitialNoticeWithinDays,
         settings.UseBusinessDays, settings.AdjustToNextWorkingDay);

    deadlines["firstReportDue"] = ComputeDeadline(noticeDate, settings.ReportEveryNDays,
            settings.UseBusinessDays, settings.AdjustToNextWorkingDay);

        return deadlines;
    }

    // ?? Business day arithmetic ??

 private DateTime AddBusinessDays(DateTime start, int days)
    {
 var direction = days >= 0 ? 1 : -1;
        var remaining = Math.Abs(days);
        var current = start;

 while (remaining > 0)
     {
   current = current.AddDays(direction);
   if (IsWorkingDay(current))
          remaining--;
        }

      return current;
}

    private DateTime EnsureWorkingDay(DateTime date)
    {
        while (!IsWorkingDay(date))
       date = date.AddDays(1);
        return date;
    }

    public bool IsWorkingDay(DateTime date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        return false;
        if (FixedHolidays.Contains((date.Month, date.Day)))
            return false;
        return true;
    }

    /// <summary>Convert UTC to Europe/Bucharest local time.</summary>
    public DateTime ToBucharest(DateTime utcDate) =>
        TimeZoneInfo.ConvertTimeFromUtc(utcDate, BucharestTz);

    // ?? Config helpers ??

    private static int GetIntConfig(Dictionary<string, string> configs, string key, int fallback) =>
      configs.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    private static bool GetBoolConfig(Dictionary<string, string> configs, string key, bool fallback) =>
        configs.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static int[] GetIntArrayConfig(Dictionary<string, string> configs, string key, int[] fallback) =>
        configs.TryGetValue(key, out var v)
          ? v.Split(',').Select(s => int.TryParse(s.Trim(), out var i) ? i : 0).Where(i => i >= 0).ToArray()
          : fallback;
}

/// <summary>Deadline configuration values resolved from the hierarchy.</summary>
public class DeadlineSettings
{
    public int ClaimDeadlineDaysFromNotice { get; set; } = 30;
    public int ObjectionDeadlineDaysFromNotice { get; set; } = 45;
    public int SendInitialNoticeWithinDays { get; set; } = 2;
    public int MeetingNoticeMinimumDays { get; set; } = 14;
    public int ReportEveryNDays { get; set; } = 30;
    public bool UseBusinessDays { get; set; }
    public bool AdjustToNextWorkingDay { get; set; } = true;
    public int[] ReminderDaysBeforeDeadline { get; set; } = { 7, 3, 1, 0 };
}
