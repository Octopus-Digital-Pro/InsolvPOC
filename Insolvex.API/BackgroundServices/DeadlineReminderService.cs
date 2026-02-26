using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Data.Services;
using Insolvex.Domain.Entities;

namespace Insolvex.API.BackgroundServices;

/// <summary>
/// Background service that checks for upcoming/overdue deadlines and:
/// - Sends reminder notifications at T-7, T-3, T-1, T-0
/// - Delegates critical deadline escalation to TaskEscalationService
/// - Creates daily digest emails
/// Runs every hour.
/// </summary>
public class DeadlineReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadlineReminderService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public DeadlineReminderService(IServiceScopeFactory scopeFactory, ILogger<DeadlineReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
_logger.LogInformation("DeadlineReminderService started");

        while (!stoppingToken.IsCancellationRequested)
        {
   try
            {
    await ProcessRemindersAsync(stoppingToken);
     }
            catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing deadline reminders");
            }

   await Task.Delay(CheckInterval, stoppingToken);
        }
  }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
      var escalationService = scope.ServiceProvider.GetRequiredService<TaskEscalationService>();

        var now = DateTime.UtcNow;
        var reminderDays = new[] { 7, 3, 1, 0 };

        foreach (var days in reminderDays)
  {
   var targetDate = now.AddDays(days).Date;
    var nextDay = targetDate.AddDays(1);

            var tasks = await db.CompanyTasks
.Where(t => t.Deadline.HasValue
      && t.Deadline.Value >= targetDate
   && t.Deadline.Value < nextDay
      && t.Status != Domain.Enums.TaskStatus.Done
          && t.Status != Domain.Enums.TaskStatus.Cancelled
   && t.AssignedToUserId.HasValue)
     .Include(t => t.Case)
           .ToListAsync(ct);

            foreach (var task in tasks)
            {
       var reminderKey = $"reminder_{task.Id}_{days}d";
                var alreadySent = await db.ScheduledEmails
  .AnyAsync(e => e.Subject != null && e.Subject.Contains(reminderKey), ct);

     if (alreadySent) continue;

      var user = await db.Users.FindAsync(new object[] { task.AssignedToUserId!.Value }, ct);
     if (user == null || string.IsNullOrWhiteSpace(user.Email)) continue;

     var urgency = days switch
           {
         0 => "?? TODAY",
           1 => "?? TOMORROW",
     3 => "?? In 3 days",
   _ => $"?? In {days} days",
         };

            var caseName = task.Case?.CaseNumber ?? "N/A";

        db.ScheduledEmails.Add(new ScheduledEmail
 {
   TenantId = task.TenantId,
    CaseId = task.CaseId,
   To = user.Email,
       Subject = $"[Insolvex] {urgency}: {task.Title} � {caseName} [{reminderKey}]",
     Body = $"Deadline reminder for task: {task.Title}\n" +
  $"Case: {caseName}\n" +
    $"Deadline: {task.Deadline:dd.MM.yyyy}\n" +
    $"Status: {task.Status}\n\n" +
      (task.IsCriticalDeadline ? "?? This is a CRITICAL deadline." : ""),
      ScheduledFor = DateTime.UtcNow,
      Status = "Scheduled",
           RelatedTaskId = task.Id,
          });
        }
        }

        await db.SaveChangesAsync(ct);

        // Delegate critical deadline escalation to TaskEscalationService
try
    {
            await escalationService.ProcessEscalationsAsync(ct);
}
  catch (Exception ex)
        {
   _logger.LogError(ex, "Error processing critical deadline escalations");
        }

        _logger.LogInformation("Deadline reminder check completed. Time: {Time}", now);
    }
}
