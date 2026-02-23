using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Domain.Entities;

namespace Insolvex.API.BackgroundServices;

/// <summary>
/// Background service that checks for upcoming/overdue deadlines and:
/// - Sends reminder notifications at T-7, T-3, T-1, T-0
/// - Escalates critical deadlines to team lead / admin
/// - Creates daily digest emails
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

        var now = DateTime.UtcNow;
        var reminderDays = new[] { 7, 3, 1, 0 };

        foreach (var days in reminderDays)
        {
     var targetDate = now.AddDays(days).Date;
   var nextDay = targetDate.AddDays(1);

   // Find tasks with deadlines on this target day that haven't been reminded
            var tasks = await db.CompanyTasks
       .Where(t => t.Deadline.HasValue
         && t.Deadline.Value >= targetDate
          && t.Deadline.Value < nextDay
           && t.Status != Domain.Enums.TaskStatus.Done
&& t.AssignedToUserId.HasValue)
 .Include(t => t.Case)
           .ToListAsync(ct);

            foreach (var task in tasks)
 {
           // Check if we already sent a reminder for this task+day combo
                var reminderKey = $"reminder_{task.Id}_{days}d";
    var alreadySent = await db.ScheduledEmails
            .AnyAsync(e => e.Subject != null && e.Subject.Contains(reminderKey), ct);

             if (alreadySent) continue;

      // Get assignee email
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
      To = user.Email,
        Subject = $"[Insolvex] {urgency}: {task.Title} — {caseName} [{reminderKey}]",
        Body = $"Deadline reminder for task: {task.Title}\n" +
          $"Case: {caseName}\n" +
               $"Deadline: {task.Deadline:dd.MM.yyyy}\n" +
               $"Status: {task.Status}\n\n" +
          (task.IsCriticalDeadline ? "?? This is a CRITICAL deadline." : ""),
    ScheduledFor = DateTime.UtcNow,
       });

                // For critical tasks at T-0 that are still not done, escalate
    if (days == 0 && task.IsCriticalDeadline)
      {
              // Find admin users in the tenant for escalation
 var admins = await db.Users
   .Where(u => u.TenantId == task.TenantId
&& u.IsActive
      && (u.Role == Domain.Enums.UserRole.GlobalAdmin || u.Role == Domain.Enums.UserRole.TenantAdmin))
    .Select(u => u.Email)
         .ToListAsync(ct);

             foreach (var adminEmail in admins.Where(e => e != user.Email))
           {
  db.ScheduledEmails.Add(new ScheduledEmail
  {
    TenantId = task.TenantId,
  To = adminEmail,
     Subject = $"[Insolvex] ESCALATION: Critical deadline reached — {task.Title} — {caseName}",
               Body = $"ESCALATION: A critical deadline has been reached and the task is not complete.\n\n" +
              $"Task: {task.Title}\n" +
     $"Case: {caseName}\n" +
     $"Assignee: {user.Email}\n" +
 $"Deadline: {task.Deadline:dd.MM.yyyy}\n" +
       $"Status: {task.Status}",
    ScheduledFor = DateTime.UtcNow,
     });
     }
       }
            }
        }

   await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deadline reminder check completed. Time: {Time}", now);
    }
}
