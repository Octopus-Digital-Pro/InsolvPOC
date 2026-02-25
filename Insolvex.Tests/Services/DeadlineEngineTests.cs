using FluentAssertions;
using Insolvex.API.Services;
using Insolvex.Domain.Entities;
using Insolvex.Tests.Helpers;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Tests.Services;

public class DeadlineEngineTests
{
  // ?? Business-day arithmetic ?????????????????????????????

  [Fact]
  public void ComputeDeadline_CalendarDays_AddsCorrectly()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    var baseDate = new DateTime(2025, 3, 10); // Monday

    var result = engine.ComputeDeadline(baseDate, 30);

    result.Should().Be(new DateTime(2025, 4, 9)); // Wednesday � a working day
  }

  [Fact]
  public void ComputeDeadline_BusinessDays_SkipsWeekends()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    var friday = new DateTime(2025, 3, 7); // Friday

    var result = engine.ComputeDeadline(friday, 5, useBusinessDays: true);

    result.Should().Be(new DateTime(2025, 3, 14)); // next Friday
  }

  [Fact]
  public void ComputeDeadline_LandsOnSaturday_AdjustsToMonday()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    // 2025-03-13 (Thu) + 2 = 2025-03-15 (Sat) ? adjusted to Mon 17
    var result = engine.ComputeDeadline(new DateTime(2025, 3, 13), 2, adjustToNextWorkingDay: true);

    result.Should().Be(new DateTime(2025, 3, 17));
  }

  [Fact]
  public void ComputeDeadline_NoAdjust_KeepsWeekend()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    var result = engine.ComputeDeadline(new DateTime(2025, 3, 13), 2, adjustToNextWorkingDay: false);

    result.Should().Be(new DateTime(2025, 3, 15)); // Saturday kept
  }

  // ?? Romanian holidays ???????????????????????????????????

  [Theory]
  [InlineData(2025, 1, 1)]   // New Year
  [InlineData(2025, 12, 1)]  // National Day
  [InlineData(2025, 12, 25)] // Christmas
  [InlineData(2025, 5, 1)]   // Labour Day
  public void IsWorkingDay_ReturnsFalse_ForRomanianHolidays(int year, int month, int day)
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);

    engine.IsWorkingDay(new DateTime(year, month, day)).Should().BeFalse();
  }

  [Fact]
  public void IsWorkingDay_ReturnsTrue_ForNormalWeekday()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);

    engine.IsWorkingDay(new DateTime(2025, 3, 12)).Should().BeTrue(); // Wednesday
  }

  [Theory]
  [InlineData(DayOfWeek.Saturday)]
  [InlineData(DayOfWeek.Sunday)]
  public void IsWorkingDay_ReturnsFalse_ForWeekends(DayOfWeek dow)
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    // Find next occurrence of this day of week from a known date
    var date = new DateTime(2025, 3, 10); // Monday
    while (date.DayOfWeek != dow) date = date.AddDays(1);

    engine.IsWorkingDay(date).Should().BeFalse();
  }

  // ?? Hierarchy resolution ????????????????????????????????

  [Fact]
  public async Task GetEffectiveSettings_ReturnsDefaults_WhenNoOverrides()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);

    var settings = await engine.GetEffectiveSettingsAsync(null, null);

    settings.ClaimDeadlineDaysFromNotice.Should().Be(30);
    settings.ObjectionDeadlineDaysFromNotice.Should().Be(45);
    settings.AdjustToNextWorkingDay.Should().BeTrue();
    settings.ReminderDaysBeforeDeadline.Should().BeEquivalentTo(new[] { 7, 3, 1, 0 });
  }

  [Fact]
  public async Task GetEffectiveSettings_AppliesTenantSettings()
  {
    using var db = TestDbFactory.Create();
    var tenantId = Guid.NewGuid();

    db.TenantDeadlineSettings.Add(new TenantDeadlineSettings
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      ClaimDeadlineDaysFromNotice = 60,
      ObjectionDeadlineDaysFromNotice = 90,
      SendInitialNoticeWithinDays = 5,
      MeetingNoticeMinimumDays = 21,
      ReportEveryNDays = 60,
      UseBusinessDays = true,
      AdjustToNextWorkingDay = false,
      ReminderDaysBeforeDeadline = "14,7,3",
      UrgentQueueHoursBeforeDeadline = 48,
      AutoAssignBackupOnCriticalOverdue = true,
    });
    await db.SaveChangesAsync();

    var engine = new DeadlineEngine(db);
    var settings = await engine.GetEffectiveSettingsAsync(null, tenantId);

    settings.ClaimDeadlineDaysFromNotice.Should().Be(60);
    settings.ObjectionDeadlineDaysFromNotice.Should().Be(90);
    settings.UseBusinessDays.Should().BeTrue();
    settings.AdjustToNextWorkingDay.Should().BeFalse();
    settings.ReminderDaysBeforeDeadline.Should().BeEquivalentTo(new[] { 14, 7, 3 });
    settings.UrgentQueueHoursBeforeDeadline.Should().Be(48);
  }

  [Fact]
  public async Task GetEffectiveSettings_CaseOverride_TakesPrecedence()
  {
    using var db = TestDbFactory.Create();
    var tenantId = Guid.NewGuid();
    var caseId = Guid.NewGuid();

    // Tenant level: 60 days
    db.TenantDeadlineSettings.Add(new TenantDeadlineSettings
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      ClaimDeadlineDaysFromNotice = 60,
      ObjectionDeadlineDaysFromNotice = 90,
    });

    // Case is in this tenant
    db.InsolvencyCases.Add(new InsolvencyCase
    {
      Id = caseId,
      TenantId = tenantId,
      CaseNumber = "TEST/2025",
      DebtorName = "Test Debtor",
    });

    // Case-level override: 15 days
    db.CaseDeadlineOverrides.Add(new CaseDeadlineOverride
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      CaseId = caseId,
      DeadlineKey = "ClaimDeadlineDaysFromNotice",
      OverrideValue = "15",
      OriginalValue = "60",
      Reason = "Urgent case, shortened deadline",
      IsActive = true,
      OverriddenAt = DateTime.UtcNow,
    });
    await db.SaveChangesAsync();

    var engine = new DeadlineEngine(db);
    var settings = await engine.GetEffectiveSettingsAsync(caseId, tenantId);

    // Case override wins
    settings.ClaimDeadlineDaysFromNotice.Should().Be(15);
    // Tenant setting still applies where no case override
    settings.ObjectionDeadlineDaysFromNotice.Should().Be(90);
  }

  [Fact]
  public async Task GetEffectiveSettings_GlobalConfig_AppliesBeforeTenant()
  {
    using var db = TestDbFactory.Create();

    db.SystemConfigs.Add(new SystemConfig
    {
      Id = Guid.NewGuid(),
      Key = "Deadlines:ClaimDeadlineDaysFromNotice",
      Value = "42",
      Group = "Deadlines",
    });
    await db.SaveChangesAsync();

    var engine = new DeadlineEngine(db);
    var settings = await engine.GetEffectiveSettingsAsync(null, null);

    settings.ClaimDeadlineDaysFromNotice.Should().Be(42);
  }

  // ?? Baseline deadline computation ???????????????????????

  [Fact]
  public async Task ComputeBaselineDeadlines_ProducesExpectedKeys()
  {
    using var db = TestDbFactory.Create();
    var engine = new DeadlineEngine(db);
    var noticeDate = new DateTime(2025, 4, 1);

    var deadlines = await engine.ComputeBaselineDeadlinesAsync(noticeDate, null);

    deadlines.Should().ContainKey("claimDeadline");
    deadlines.Should().ContainKey("objectionDeadline");
    deadlines.Should().ContainKey("initialNoticeSendBy");
    deadlines.Should().ContainKey("firstReportDue");

    deadlines["claimDeadline"].Should().BeAfter(noticeDate);
    deadlines["objectionDeadline"].Should().BeAfter(deadlines["claimDeadline"]);
  }
}
