using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Insolvex.Core.Services;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Insolvex.Tests.Helpers;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Tests.Services;

public class TaskEscalationServiceTests
{
  private static readonly Guid TenantId = Guid.NewGuid();

  private static (TaskEscalationService svc, Insolvex.Data.ApplicationDbContext db) CreateService()
  {
    var db = TestDbFactory.Create();
    var deadlineEngine = new DeadlineEngine(db);
    var svc = new TaskEscalationService(db, deadlineEngine, Mock.Of<ILogger<TaskEscalationService>>());
    return (svc, db);
  }

  private static void SeedUsers(Insolvex.Data.ApplicationDbContext db, out Guid practitionerId, out Guid adminId, out Guid partnerId)
  {
    practitionerId = Guid.NewGuid();
    adminId = Guid.NewGuid();
    partnerId = Guid.NewGuid();

    db.Users.AddRange(
 new User { Id = practitionerId, TenantId = TenantId, Email = "pract@test.local", FirstName = "Jon", LastName = "Doe", PasswordHash = "x", Role = UserRole.Practitioner, IsActive = true },
    new User { Id = adminId, TenantId = TenantId, Email = "admin@test.local", FirstName = "Admin", LastName = "User", PasswordHash = "x", Role = UserRole.TenantAdmin, IsActive = true },
       new User { Id = partnerId, TenantId = TenantId, Email = "partner@test.local", FirstName = "Partner", LastName = "User", PasswordHash = "x", Role = UserRole.Partner, IsActive = true }
    );
  }

  // ⚠️ Overdue task → admin escalation ─────────────────────

  [Fact]
  public async Task ProcessEscalations_OverdueTask_EscalatesToAdmins()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out var adminId, out _);

    var taskId = Guid.NewGuid();
    db.CompanyTasks.Add(new CompanyTask
    {
      Id = taskId,
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Generate meeting notice",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(-2), // overdue
      Status = TaskStatus.Open,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    var escalationEmails = db.ScheduledEmails.Where(e => e.Subject != null && e.Subject.Contains("ESCALATION")).ToList();
    escalationEmails.Should().NotBeEmpty();
    escalationEmails.Should().Contain(e => e.To == "admin@test.local");
  }

  [Fact]
  public async Task ProcessEscalations_OverdueTask_MarksAsOverdue()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out _, out _);

    var taskId = Guid.NewGuid();
    db.CompanyTasks.Add(new CompanyTask
    {
      Id = taskId,
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Critical task",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(-1),
      Status = TaskStatus.Open,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    var task = db.CompanyTasks.First(t => t.Id == taskId);
    task.Status.Should().Be(TaskStatus.Overdue);
  }

  // ⏰ Urgent window → team lead escalation ────────────────

  [Fact]
  public async Task ProcessEscalations_WithinUrgentWindow_EscalatesToTeamLead()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out _, out var partnerId);

    // Default urgentThreshold = 24 hours; set deadline at 12 hours from now
    db.CompanyTasks.Add(new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Approaching deadline",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(12),
      Status = TaskStatus.InProgress,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    var urgentEmails = db.ScheduledEmails.Where(e => e.Subject != null && e.Subject.Contains("URGENT")).ToList();
    urgentEmails.Should().NotBeEmpty();
    urgentEmails.Should().Contain(e => e.To == "partner@test.local");
  }

  // 🔄 Auto-reassign backup ────────────────────────────────

  [Fact]
  public async Task ProcessEscalations_AutoAssignsBackup_WhenConfigured()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out var adminId, out _);

    // Enable auto-assign with complete settings
    db.TenantDeadlineSettings.Add(new TenantDeadlineSettings
    {
      Id = Guid.NewGuid(),
      TenantId = TenantId,
      AutoAssignBackupOnCriticalOverdue = true,
      ClaimDeadlineDaysFromNotice = 30,
      ObjectionDeadlineDaysFromNotice = 45,
      SendInitialNoticeWithinDays = 2,
      MeetingNoticeMinimumDays = 14,
      ReportEveryNDays = 30,
      UrgentQueueHoursBeforeDeadline = 24,
    });

    var caseId = Guid.NewGuid();
    db.InsolvencyCases.Add(new InsolvencyCase
    {
      Id = caseId,
      TenantId = TenantId,
      CaseNumber = "ESC/001",
      DebtorName = "Test Debtor",
    });

    var taskId = Guid.NewGuid();
    db.CompanyTasks.Add(new CompanyTask
    {
      Id = taskId,
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      CaseId = caseId,
      Title = "Overdue critical",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(-5),
      Status = TaskStatus.Open,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    var task = db.CompanyTasks.First(t => t.Id == taskId);
    task.AssignedToUserId.Should().NotBe(practId, "should be reassigned to backup");
    task.AssignedToUserId.Should().Be(adminId);

    // Audit log created
    var auditLog = db.AuditLogs.FirstOrDefault(a => a.Action == "Task.AutoReassigned");
    auditLog.Should().NotBeNull();
  }

  // ?? Non-critical tasks are NOT escalated ????????????????

  [Fact]
  public async Task ProcessEscalations_SkipsNonCriticalTasks()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out _, out _);

    db.CompanyTasks.Add(new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Normal task",
      IsCriticalDeadline = false, // not critical
      Deadline = DateTime.UtcNow.AddHours(-5),
      Status = TaskStatus.Open,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    db.ScheduledEmails.Count().Should().Be(0, "non-critical tasks should not be escalated");
  }

  // ?? Done/Cancelled tasks are NOT escalated ??????????????

  [Theory]
  [InlineData(TaskStatus.Done)]
  [InlineData(TaskStatus.Cancelled)]
  public async Task ProcessEscalations_SkipsCompletedOrCancelledTasks(TaskStatus status)
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out _, out _);

    db.CompanyTasks.Add(new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Done/cancelled critical",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(-5),
      Status = status,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    await svc.ProcessEscalationsAsync(CancellationToken.None);

    db.ScheduledEmails.Count().Should().Be(0);
  }

  // ?? Idempotency: don't double-escalate ??????????????????

  [Fact]
  public async Task ProcessEscalations_DoesNotDoubleEscalate()
  {
    var (svc, db) = CreateService();
    SeedUsers(db, out var practId, out _, out _);

    db.CompanyTasks.Add(new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = TenantId,
      CompanyId = Guid.NewGuid(),
      Title = "Critical overdue",
      IsCriticalDeadline = true,
      Deadline = DateTime.UtcNow.AddHours(-2),
      Status = TaskStatus.Open,
      AssignedToUserId = practId,
    });
    await db.SaveChangesAsync();

    // Run twice
    await svc.ProcessEscalationsAsync(CancellationToken.None);
    await svc.ProcessEscalationsAsync(CancellationToken.None);

    var escalationCount = db.ScheduledEmails.Count(e => e.Subject != null && e.Subject.Contains("ESCALATION"));
    escalationCount.Should().Be(1, "should not create duplicate escalation emails on same day");
  }
}
