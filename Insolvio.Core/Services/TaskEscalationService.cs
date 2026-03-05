using Microsoft.EntityFrameworkCore;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;
using TaskStatus = Insolvio.Domain.Enums.TaskStatus;

namespace Insolvio.Core.Services;

/// <summary>
/// Handles escalation of critical deadline tasks per InsolvencyAppRules section 3:
///   - Auto-escalation: assignee ? team lead ? partner/admin
///   - Urgent queue trigger when deadline is within X hours and task still blocked
///   - Optional auto-assign backup user
///   
/// TODO: Integrate with notification service for real-time push notifications.
/// TODO: Add configurable escalation policies per tenant.
/// </summary>
public class TaskEscalationService
{
   private readonly IApplicationDbContext _db;
   private readonly DeadlineEngine _deadlineEngine;
   private readonly ILogger<TaskEscalationService> _logger;

   public TaskEscalationService(
  IApplicationDbContext db,
     DeadlineEngine deadlineEngine,
 ILogger<TaskEscalationService> logger)
   {
      _db = db;
      _deadlineEngine = deadlineEngine;
      _logger = logger;
   }

   /// <summary>
   /// Process escalations for all critical tasks approaching or past their deadline.
   /// Called by the DeadlineReminderService background worker.
   /// </summary>
   public async Task ProcessEscalationsAsync(CancellationToken ct)
   {
      var now = DateTime.UtcNow;

      // Find critical tasks that are not done and deadline is approaching
      var criticalTasks = await _db.CompanyTasks
      .Where(t => t.IsCriticalDeadline
      && t.Deadline.HasValue
      && t.Status != TaskStatus.Done
             && t.Status != TaskStatus.Cancelled
                && t.AssignedToUserId.HasValue)
       .Include(t => t.Case)
             .Include(t => t.AssignedTo)
 .ToListAsync(ct);

      foreach (var task in criticalTasks)
      {
         if (ct.IsCancellationRequested) break;

         var hoursUntilDeadline = (task.Deadline!.Value - now).TotalHours;

         // Get tenant-level settings for urgent threshold
         var settings = await _deadlineEngine.GetEffectiveSettingsAsync(task.CaseId, null);
         var urgentThresholdHours = settings.UrgentQueueHoursBeforeDeadline;

         if (hoursUntilDeadline <= 0)
         {
            // Deadline passed — escalate to admin
            await EscalateToAdminAsync(task, "OVERDUE", ct);

            // Auto-assign backup if configured
            if (settings.AutoAssignBackupOnCriticalOverdue)
               await TryAutoAssignBackupAsync(task, ct);

            // Mark as overdue
            if (task.Status != TaskStatus.Overdue)
            {
               task.Status = TaskStatus.Overdue;
            }
         }
         else if (hoursUntilDeadline <= urgentThresholdHours)
         {
            // Within urgent window — escalate to team lead
            await EscalateToTeamLeadAsync(task, hoursUntilDeadline, ct);
         }
      }

      await _db.SaveChangesAsync(ct);
   }

   /// <summary>
   /// Escalate a task to all tenant admins.
   /// </summary>
   private async Task EscalateToAdminAsync(CompanyTask task, string urgencyLevel, CancellationToken ct)
   {
      var escalationKey = $"escalation_{task.Id}_admin_{DateTime.UtcNow:yyyyMMdd}";

      // Check if already escalated today
      var alreadySent = await _db.ScheduledEmails
         .AnyAsync(e => e.Subject != null && e.Subject.Contains(escalationKey), ct);
      if (alreadySent) return;

      var admins = await _db.Users
    .Where(u => u.TenantId == task.TenantId
         && u.IsActive
        && (u.Role == UserRole.GlobalAdmin || u.Role == UserRole.TenantAdmin))
           .ToListAsync(ct);

      var caseName = task.Case?.CaseNumber ?? "N/A";
      var assigneeName = task.AssignedTo?.FullName ?? "Unassigned";

      foreach (var admin in admins)
      {
         if (admin.Email == task.AssignedTo?.Email) continue; // Don't double-notify

         _db.ScheduledEmails.Add(new ScheduledEmail
         {
            TenantId = task.TenantId,
            CaseId = task.CaseId,
            To = admin.Email,
            Subject = $"[Insolvio] ESCALATION {urgencyLevel}: {task.Title} — {caseName} [{escalationKey}]",
            Body = $"<h3>Critical Deadline Escalation</h3>" +
$"<p><strong>Task:</strong> {task.Title}</p>" +
   $"<p><strong>Case:</strong> {caseName}</p>" +
$"<p><strong>Assignee:</strong> {assigneeName}</p>" +
   $"<p><strong>Deadline:</strong> {task.Deadline:dd.MM.yyyy HH:mm}</p>" +
   $"<p><strong>Status:</strong> {task.Status}</p>" +
$"<p style='color:red'><strong>This is a critical deadline that has been {urgencyLevel}.</strong></p>",
            ScheduledFor = DateTime.UtcNow,
            Status = "Scheduled",
            IsHtml = true,
            RelatedTaskId = task.Id,
         });
      }

      _logger.LogWarning("Escalated critical task {TaskId} ({Title}) to {Count} admins — {Level}",
     task.Id, task.Title, admins.Count, urgencyLevel);
   }

   /// <summary>
   /// Escalate a task to the team lead (Partner role users in the tenant).
   /// </summary>
   private async Task EscalateToTeamLeadAsync(CompanyTask task, double hoursRemaining, CancellationToken ct)
   {
      var escalationKey = $"escalation_{task.Id}_lead_{DateTime.UtcNow:yyyyMMdd}";

      var alreadySent = await _db.ScheduledEmails
.AnyAsync(e => e.Subject != null && e.Subject.Contains(escalationKey), ct);
      if (alreadySent) return;

      var leads = await _db.Users
       .Where(u => u.TenantId == task.TenantId
               && u.IsActive
    && (u.Role == UserRole.Partner || u.Role == UserRole.TenantAdmin))
            .ToListAsync(ct);

      var caseName = task.Case?.CaseNumber ?? "N/A";

      foreach (var lead in leads)
      {
         if (lead.Email == task.AssignedTo?.Email) continue;

         _db.ScheduledEmails.Add(new ScheduledEmail
         {
            TenantId = task.TenantId,
            CaseId = task.CaseId,
            To = lead.Email,
            Subject = $"[Insolvio] URGENT: {task.Title} — {caseName} ({hoursRemaining:F0}h remaining) [{escalationKey}]",
            Body = $"<h3>Urgent Deadline Warning</h3>" +
        $"<p><strong>Task:</strong> {task.Title}</p>" +
      $"<p><strong>Case:</strong> {caseName}</p>" +
    $"<p><strong>Hours remaining:</strong> {hoursRemaining:F1}</p>" +
           $"<p><strong>Deadline:</strong> {task.Deadline:dd.MM.yyyy HH:mm}</p>" +
        $"<p>Please ensure this task is completed before the deadline.</p>",
            ScheduledFor = DateTime.UtcNow,
            Status = "Scheduled",
            IsHtml = true,
            RelatedTaskId = task.Id,
         });
      }
   }

   /// <summary>
   /// Try to auto-assign the task to a backup user (admin) if the current assignee hasn't completed it.
   /// </summary>
   private async Task TryAutoAssignBackupAsync(CompanyTask task, CancellationToken ct)
   {
      // Only auto-assign if still with original assignee and overdue
      if (task.Status == TaskStatus.Done || task.Status == TaskStatus.Cancelled) return;

      // Find an available admin/partner who isn't the current assignee
      var backup = await _db.Users
    .Where(u => u.TenantId == task.TenantId
  && u.IsActive
      && u.Id != task.AssignedToUserId
      && (u.Role == UserRole.TenantAdmin || u.Role == UserRole.Partner))
   .FirstOrDefaultAsync(ct);

      if (backup == null) return;

      var previousAssignee = task.AssignedToUserId;
      task.AssignedToUserId = backup.Id;

      _db.AuditLogs.Add(new AuditLog
      {
         TenantId = task.TenantId,
         Action = "Task.AutoReassigned",
         EntityType = "CompanyTask",
         EntityId = task.Id,
         Description = $"Critical overdue task auto-reassigned from {previousAssignee} to {backup.Email}",
         UserEmail = "System",
         Severity = "Critical",
         Category = "Task",
         Timestamp = DateTime.UtcNow,
      });

      _logger.LogWarning("Auto-reassigned critical overdue task {TaskId} to backup user {BackupEmail}",
          task.Id, backup.Email);
   }
}
