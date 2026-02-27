using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.API.BackgroundServices;

/// <summary>
/// Background service that enforces critical template deadlines per InsolvencyAppRules section 7:
///   - Checks for GeneratedLetters with IsCritical=true and approaching SendDeadline
///   - Creates blocking tasks if generation failed
///   - Escalates if send deadline is approaching and letter is still pending
///   - Runs every 30 minutes
/// 
/// TODO: Implement actual template auto-generation triggers.
///       For now this monitors existing GeneratedLetter records.
/// </summary>
public class TemplateEnforcementService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<TemplateEnforcementService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    public TemplateEnforcementService(IServiceScopeFactory scopeFactory, ILogger<TemplateEnforcementService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TemplateEnforcementService started");

        while (!stoppingToken.IsCancellationRequested)
        {
    try
    {
                await ProcessCriticalTemplatesAsync(stoppingToken);
            }
   catch (Exception ex)
   {
       _logger.LogError(ex, "Error in TemplateEnforcementService");
        }

      await Task.Delay(CheckInterval, stoppingToken);
     }
    }

    private async Task ProcessCriticalTemplatesAsync(CancellationToken ct)
    {
     using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var now = DateTime.UtcNow;

     // Find critical letters that are pending and have a send deadline
        var pendingCritical = await db.GeneratedLetters
        .Where(g => g.IsCritical
           && g.DeliveryStatus == "Pending"
           && g.SendDeadline.HasValue
            && g.SendDeadline.Value > now.AddDays(-1)) // Don't process ancient ones
       .Include(g => g.Case)
        .ToListAsync(ct);

   foreach (var letter in pendingCritical)
     {
   if (ct.IsCancellationRequested) break;

       var hoursUntilDeadline = (letter.SendDeadline!.Value - now).TotalHours;

     if (hoursUntilDeadline <= 0)
  {
    // Deadline passed — create escalation
       await CreateEscalationAsync(db, letter, "OVERDUE: Critical template send deadline passed", ct);
      }
    else if (hoursUntilDeadline <= 24)
  {
  // Within 24 hours — create warning
      await CreateWarningAsync(db, letter, hoursUntilDeadline, ct);
            }
   else if (hoursUntilDeadline <= 72)
   {
          // Within 3 days — ensure a generation task exists
       await EnsureGenerationTaskExistsAsync(db, letter, ct);
            }
     }

        // Find failed generation letters
        var failedLetters = await db.GeneratedLetters
            .Where(g => g.IsCritical && g.DeliveryStatus == "Failed")
 .Include(g => g.Case)
.ToListAsync(ct);

    foreach (var letter in failedLetters)
      {
    // Create a blocking "Fix merge fields" task
    await CreateBlockingFixTaskAsync(db, letter, ct);
   }

      await db.SaveChangesAsync(ct);
        _logger.LogInformation("Template enforcement check completed at {Time}", now);
    }

    private static async Task CreateEscalationAsync(
 ApplicationDbContext db, GeneratedLetter letter, string message, CancellationToken ct)
    {
        var escalationKey = $"template_esc_{letter.Id}_{DateTime.UtcNow:yyyyMMdd}";

        var alreadyExists = await db.ScheduledEmails
    .AnyAsync(e => e.Subject != null && e.Subject.Contains(escalationKey), ct);
        if (alreadyExists) return;

        // Find admins in the tenant
      var admins = await db.Users
     .Where(u => u.TenantId == letter.TenantId
     && u.IsActive
     && (u.Role == UserRole.GlobalAdmin || u.Role == UserRole.TenantAdmin))
     .Select(u => u.Email)
      .ToListAsync(ct);

   var caseName = letter.Case?.CaseNumber ?? "N/A";

  foreach (var adminEmail in admins)
  {
db.ScheduledEmails.Add(new ScheduledEmail
         {
          TenantId = letter.TenantId,
 CaseId = letter.CaseId,
     To = adminEmail,
    Subject = $"[Insolvex] CRITICAL TEMPLATE {message} — {caseName} [{escalationKey}]",
     Body = $"<h3>Critical Template Deadline</h3>" +
      $"<p><strong>Template:</strong> {letter.TemplateType} — {letter.FileName}</p>" +
   $"<p><strong>Case:</strong> {caseName}</p>" +
           $"<p><strong>Send Deadline:</strong> {letter.SendDeadline:dd.MM.yyyy HH:mm}</p>" +
  $"<p><strong>Status:</strong> {letter.DeliveryStatus}</p>" +
     $"<p style='color:red'><strong>{message}</strong></p>",
    ScheduledFor = DateTime.UtcNow,
    Status = "Scheduled",
        IsHtml = true,
          });
}
    }

    private static async Task CreateWarningAsync(
     ApplicationDbContext db, GeneratedLetter letter, double hoursRemaining, CancellationToken ct)
    {
    var warningKey = $"template_warn_{letter.Id}_{DateTime.UtcNow:yyyyMMdd}";

        var alreadyExists = await db.ScheduledEmails
       .AnyAsync(e => e.Subject != null && e.Subject.Contains(warningKey), ct);
     if (alreadyExists) return;

      // Get case owner
  var caseOwnerEmail = await db.InsolvencyCases
   .Where(c => c.Id == letter.CaseId)
     .Select(c => c.AssignedTo != null ? c.AssignedTo.Email : null)
   .FirstOrDefaultAsync(ct);

     if (string.IsNullOrEmpty(caseOwnerEmail)) return;

  var caseName = letter.Case?.CaseNumber ?? "N/A";

        db.ScheduledEmails.Add(new ScheduledEmail
        {
   TenantId = letter.TenantId,
     CaseId = letter.CaseId,
        To = caseOwnerEmail,
    Subject = $"[Insolvex] URGENT: Template send deadline in {hoursRemaining:F0}h — {caseName} [{warningKey}]",
      Body = $"<h3>Template Send Deadline Approaching</h3>" +
     $"<p><strong>Template:</strong> {letter.TemplateType} — {letter.FileName}</p>" +
     $"<p><strong>Case:</strong> {caseName}</p>" +
  $"<p><strong>Hours remaining:</strong> {hoursRemaining:F1}</p>" +
        $"<p>Please ensure this document is generated, reviewed, and sent before the deadline.</p>",
    ScheduledFor = DateTime.UtcNow,
            Status = "Scheduled",
  IsHtml = true,
        });
    }

    private static async Task EnsureGenerationTaskExistsAsync(
  ApplicationDbContext db, GeneratedLetter letter, CancellationToken ct)
    {
        // Check if a task already exists for this letter
    if (letter.RelatedTaskId.HasValue)
     {
      var existingTask = await db.CompanyTasks.FindAsync(new object[] { letter.RelatedTaskId.Value }, ct);
  if (existingTask != null && existingTask.Status != TaskStatus.Done)
         return; // Task exists and is still open
      }

  // No task exists — find case to get company context
        var caseEntity = await db.InsolvencyCases.FindAsync(new object[] { letter.CaseId }, ct);
 if (caseEntity?.CompanyId == null) return;

 var task = new CompanyTask
     {
            Id = Guid.NewGuid(),
       TenantId = letter.TenantId,
    CompanyId = caseEntity.CompanyId.Value,
       CaseId = letter.CaseId,
   Title = $"Generate template: {letter.TemplateType} — {caseEntity.CaseNumber}",
      Description = $"Critical template {letter.FileName} must be generated and sent by {letter.SendDeadline:dd.MM.yyyy}.",
 Category = "Document",
            Deadline = letter.SendDeadline?.AddDays(-1) ?? DateTime.UtcNow.AddDays(1),
          DeadlineSource = "CompanyDefault",
      IsCriticalDeadline = true,
         Status = TaskStatus.Open,
 AssignedToUserId = caseEntity.AssignedToUserId,
        };

        db.CompanyTasks.Add(task);
        letter.RelatedTaskId = task.Id;
    }

    private static async Task CreateBlockingFixTaskAsync(
   ApplicationDbContext db, GeneratedLetter letter, CancellationToken ct)
    {
        // Check if a fix task already exists
        var existingFix = await db.CompanyTasks
    .AnyAsync(t => t.CaseId == letter.CaseId
          && t.Title.Contains("Fix merge fields")
   && t.Title.Contains(letter.TemplateType.ToString())
            && t.Status != TaskStatus.Done, ct);

   if (existingFix) return;

        var caseEntity = await db.InsolvencyCases.FindAsync(new object[] { letter.CaseId }, ct);
  if (caseEntity?.CompanyId == null) return;

    db.CompanyTasks.Add(new CompanyTask
        {
  Id = Guid.NewGuid(),
 TenantId = letter.TenantId,
       CompanyId = caseEntity.CompanyId.Value,
    CaseId = letter.CaseId,
            Title = $"Fix merge fields: {letter.TemplateType} — {caseEntity.CaseNumber}",
      Description = $"Template generation failed: {letter.ErrorMessage}. Fix the missing/incorrect merge fields and retry.",
  Category = "Document",
   Deadline = letter.SendDeadline ?? DateTime.UtcNow.AddDays(1),
  DeadlineSource = "CompanyDefault",
      IsCriticalDeadline = true,
         Status = TaskStatus.Blocked,
         AssignedToUserId = caseEntity.AssignedToUserId,
     });
    }
}
