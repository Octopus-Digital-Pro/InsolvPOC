using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

/// <summary>
/// Computes deadlines from NoticeDate + configurable periods.
/// Supports calendar days, business days, and "next working day if weekend/holiday".
/// Fixed to Europe/Bucharest timezone.
/// 
/// Resolution hierarchy (per InsolvencyAppRules section 3):
///   1. Case-level override (CaseDeadlineOverride) � highest priority
///   2. Tenant-level settings (TenantDeadlineSettings)
///   3. Global defaults (SystemConfig "Deadlines:" keys)
///   4. Hardcoded fallback
///   
/// Deadline sources (per InsolvencyAppRules section 3):
///   1. Notice-derived explicit date (highest priority)
/// 2. Computed from NoticeDate + period (Company default / Case-type default)
///   3. Manual override (requires reason + audit log)
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
        (8, 15),  // Assumption of Mary
        (11, 30),         // St. Andrew's Day
        (12, 1),     // National Day
        (12, 25), (12, 26), // Christmas
    };

  public DeadlineEngine(ApplicationDbContext db) => _db = db;

  // ?? Public API ??????????????????????????????????????????

  /// <summary>
  /// Compute a deadline from a base date + period.
  /// </summary>
  public DateTime ComputeDeadline(
      DateTime baseDate,
      int days,
      bool useBusinessDays = false,
      bool adjustToNextWorkingDay = true)
  {
    DateTime result;
    if (useBusinessDays)
      result = AddBusinessDays(baseDate, days);
    else
      result = baseDate.AddDays(days);

    if (adjustToNextWorkingDay)
      result = EnsureWorkingDay(result);

    return result;
  }

  /// <summary>
  /// Get the effective deadline settings for a case using the full resolution hierarchy:
  /// case override ? tenant settings ? global defaults ? hardcoded fallback.
  /// </summary>
  public async Task<DeadlineSettings> GetEffectiveSettingsAsync(Guid? caseId, Guid? tenantId)
  {
    // Start with hardcoded defaults
    var settings = new DeadlineSettings();

    // Layer 1: Global defaults from SystemConfig
    var globalConfigs = await _db.SystemConfigs.AsNoTracking()
.Where(c => c.Group == "Deadlines")
        .ToDictionaryAsync(c => c.Key, c => c.Value);

    ApplyGlobalConfigs(settings, globalConfigs);

    // Layer 2: Tenant-level settings (override globals)
    if (tenantId.HasValue)
    {
      var tenantSettings = await _db.TenantDeadlineSettings.AsNoTracking()
  .FirstOrDefaultAsync(t => t.TenantId == tenantId.Value);

      if (tenantSettings != null)
        ApplyTenantSettings(settings, tenantSettings);
    }
    // If no tenantId but we have caseId, resolve tenant from the case
    else if (caseId.HasValue)
    {
      var caseTenantId = await _db.InsolvencyCases.AsNoTracking()
.Where(c => c.Id == caseId.Value)
.Select(c => c.TenantId)
  .FirstOrDefaultAsync();

      if (caseTenantId != Guid.Empty)
      {
        var tenantSettings = await _db.TenantDeadlineSettings.AsNoTracking()
           .FirstOrDefaultAsync(t => t.TenantId == caseTenantId);

        if (tenantSettings != null)
          ApplyTenantSettings(settings, tenantSettings);
      }
    }

    // Layer 3: Case-level overrides (highest priority)
    if (caseId.HasValue)
    {
      var overrides = await _db.CaseDeadlineOverrides.AsNoTracking()
.Where(o => o.CaseId == caseId.Value && o.IsActive)
.ToListAsync();

      ApplyCaseOverrides(settings, overrides);
    }

    return settings;
  }

  /// <summary>
  /// Compute all baseline deadlines for a new case from NoticeDate.
  /// Uses the full hierarchy resolution.
  /// </summary>
  public async Task<Dictionary<string, DateTime>> ComputeBaselineDeadlinesAsync(
      DateTime noticeDate, Guid? tenantId, Guid? caseId = null)
  {
    var settings = await GetEffectiveSettingsAsync(caseId, tenantId);
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

  /// <summary>
  /// Get the reminder schedule (days before deadline) for the given context.
  /// </summary>
  public async Task<int[]> GetReminderDaysAsync(Guid? caseId, Guid? tenantId)
  {
    var settings = await GetEffectiveSettingsAsync(caseId, tenantId);
    return settings.ReminderDaysBeforeDeadline;
  }

  // ?? Business day arithmetic ?????????????????????????????

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

  // ?? Hierarchy application helpers ???????????????????????

  private static void ApplyGlobalConfigs(DeadlineSettings settings, Dictionary<string, string> configs)
  {
    settings.ClaimDeadlineDaysFromNotice = GetInt(configs, "Deadlines:ClaimDeadlineDaysFromNotice", settings.ClaimDeadlineDaysFromNotice);
    settings.ObjectionDeadlineDaysFromNotice = GetInt(configs, "Deadlines:ObjectionDeadlineDaysFromNotice", settings.ObjectionDeadlineDaysFromNotice);
    settings.SendInitialNoticeWithinDays = GetInt(configs, "Deadlines:SendInitialNoticeWithinDays", settings.SendInitialNoticeWithinDays);
    settings.MeetingNoticeMinimumDays = GetInt(configs, "Deadlines:MeetingNoticeMinimumDays", settings.MeetingNoticeMinimumDays);
    settings.ReportEveryNDays = GetInt(configs, "Deadlines:ReportEveryNDays", settings.ReportEveryNDays);
    settings.UseBusinessDays = GetBool(configs, "Deadlines:UseBusinessDays", settings.UseBusinessDays);
    settings.AdjustToNextWorkingDay = GetBool(configs, "Deadlines:AdjustToNextWorkingDay", settings.AdjustToNextWorkingDay);
    settings.ReminderDaysBeforeDeadline = GetIntArray(configs, "Deadlines:ReminderDays", settings.ReminderDaysBeforeDeadline);
  }

  private static void ApplyTenantSettings(DeadlineSettings settings, TenantDeadlineSettings tenant)
  {
    settings.ClaimDeadlineDaysFromNotice = tenant.ClaimDeadlineDaysFromNotice;
    settings.ObjectionDeadlineDaysFromNotice = tenant.ObjectionDeadlineDaysFromNotice;
    settings.SendInitialNoticeWithinDays = tenant.SendInitialNoticeWithinDays;
    settings.MeetingNoticeMinimumDays = tenant.MeetingNoticeMinimumDays;
    settings.ReportEveryNDays = tenant.ReportEveryNDays;
    settings.UseBusinessDays = tenant.UseBusinessDays;
    settings.AdjustToNextWorkingDay = tenant.AdjustToNextWorkingDay;
    settings.ReminderDaysBeforeDeadline = ParseIntArray(tenant.ReminderDaysBeforeDeadline, settings.ReminderDaysBeforeDeadline);
    settings.UrgentQueueHoursBeforeDeadline = tenant.UrgentQueueHoursBeforeDeadline;
    settings.AutoAssignBackupOnCriticalOverdue = tenant.AutoAssignBackupOnCriticalOverdue;
  }

  private static void ApplyCaseOverrides(DeadlineSettings settings, List<CaseDeadlineOverride> overrides)
  {
    foreach (var o in overrides)
    {
      if (!int.TryParse(o.OverrideValue, out var intVal)) continue;

      switch (o.DeadlineKey)
      {
        case "ClaimDeadlineDaysFromNotice":
          settings.ClaimDeadlineDaysFromNotice = intVal;
          break;
        case "ObjectionDeadlineDaysFromNotice":
          settings.ObjectionDeadlineDaysFromNotice = intVal;
          break;
        case "SendInitialNoticeWithinDays":
          settings.SendInitialNoticeWithinDays = intVal;
          break;
        case "MeetingNoticeMinimumDays":
          settings.MeetingNoticeMinimumDays = intVal;
          break;
        case "ReportEveryNDays":
          settings.ReportEveryNDays = intVal;
          break;
      }
    }
  }

  // ?? Config parsing helpers ??????????????????????????????

  private static int GetInt(Dictionary<string, string> configs, string key, int fallback) =>
  configs.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

  private static bool GetBool(Dictionary<string, string> configs, string key, bool fallback) =>
      configs.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

  private static int[] GetIntArray(Dictionary<string, string> configs, string key, int[] fallback) =>
   configs.TryGetValue(key, out var v) ? ParseIntArray(v, fallback) : fallback;

  private static int[] ParseIntArray(string? csv, int[] fallback)
  {
    if (string.IsNullOrWhiteSpace(csv)) return fallback;
    var parsed = csv.Split(',')
        .Select(s => int.TryParse(s.Trim(), out var i) ? i : -1)
.Where(i => i >= 0)
 .ToArray();
    return parsed.Length > 0 ? parsed : fallback;
  }
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
  public int UrgentQueueHoursBeforeDeadline { get; set; } = 24;
  public bool AutoAssignBackupOnCriticalOverdue { get; set; }
}
