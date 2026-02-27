using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Insolvex.Data;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

/// <summary>
/// Orchestrates the "Call creditor meeting" sidebar action.
/// Creates calendar event, generates notice tasks, schedules email sends,
/// and triggers MailMerge for meeting notice pack (GeneratedLetter records).
/// </summary>
public class CreditorMeetingService
{
  private readonly ApplicationDbContext _db;
  private readonly DeadlineEngine _deadlineEngine;
  private readonly MailMergeService _mailMerge;
  private readonly ILogger<CreditorMeetingService> _logger;

  public CreditorMeetingService(
      ApplicationDbContext db,
      DeadlineEngine deadlineEngine,
      MailMergeService mailMerge,
   ILogger<CreditorMeetingService> logger)
  {
    _db = db;
    _deadlineEngine = deadlineEngine;
    _mailMerge = mailMerge;
    _logger = logger;
  }

  public async Task<CreditorMeetingResult> CreateMeetingAsync(CreateMeetingRequest request, Guid userId, Guid tenantId)
  {
    var caseEntity = await _db.InsolvencyCases
   .Include(c => c.Parties).ThenInclude(p => p.Company)
       .Include(c => c.Company)
          .FirstOrDefaultAsync(c => c.Id == request.CaseId);

    if (caseEntity == null)
      return new CreditorMeetingResult { Error = "Case not found" };

    var settings = await _deadlineEngine.GetEffectiveSettingsAsync(request.CaseId, tenantId);

    // 1. Create calendar event
    var calendarEvent = new CalendarEvent
    {
      TenantId = tenantId,
      CaseId = request.CaseId,
      Title = $"Creditor Meeting — {caseEntity.CaseNumber}",
      Description = request.Agenda,
      Start = request.MeetingDate,
      End = request.MeetingDate.AddHours(request.DurationHours ?? 2),
      Location = request.Location,
      EventType = "Meeting",
      ParticipantsJson = System.Text.Json.JsonSerializer.Serialize(
caseEntity.Parties
.Where(p => p.Role is CasePartyRole.SecuredCreditor or CasePartyRole.UnsecuredCreditor
   or CasePartyRole.BudgetaryCreditor or CasePartyRole.EmployeeCreditor)
 .Select(p => new { name = p.Name ?? p.Company?.Name, email = p.Email ?? p.Company?.Email, role = p.Role.ToString() })),
    };
    _db.CalendarEvents.Add(calendarEvent);

    // 2. Compute notice send deadline
    var noticeSendDeadline = _deadlineEngine.ComputeDeadline(
            request.MeetingDate, -settings.MeetingNoticeMinimumDays,
      useBusinessDays: false, adjustToNextWorkingDay: false);

    // 3. Create tasks
    var companyId = caseEntity.CompanyId ?? caseEntity.Parties.FirstOrDefault()?.CompanyId ?? Guid.Empty;

    var tasks = new List<CompanyTask>
        {
         new()
            {
   TenantId = tenantId, CompanyId = companyId, CaseId = request.CaseId,
        Title = $"Generate meeting notice pack — {caseEntity.DebtorName}",
                Category = "Meeting",
    Deadline = noticeSendDeadline, DeadlineSource = "CompanyDefault",
                IsCriticalDeadline = true, AssignedToUserId = userId, CreatedByUserId = userId,
        },
       new()
       {
     TenantId = tenantId, CompanyId = companyId, CaseId = request.CaseId,
       Title = $"Send meeting invites/notices — {caseEntity.DebtorName}",
    Category = "Email",
       Deadline = noticeSendDeadline, DeadlineSource = "CompanyDefault",
                IsCriticalDeadline = true, AssignedToUserId = userId, CreatedByUserId = userId,
     },
            new()
            {
         TenantId = tenantId, CompanyId = companyId, CaseId = request.CaseId,
                Title = $"Prepare voting register — {caseEntity.DebtorName}",
    Category = "Document",
    Deadline = request.MeetingDate.AddDays(-1), DeadlineSource = "CompanyDefault",
   AssignedToUserId = userId, CreatedByUserId = userId,
    },
      new()
            {
         TenantId = tenantId, CompanyId = companyId, CaseId = request.CaseId,
                Title = $"Record attendance and votes — {caseEntity.DebtorName}",
   Category = "Meeting",
Deadline = request.MeetingDate.AddDays(1), DeadlineSource = "Manual",
     AssignedToUserId = userId, CreatedByUserId = userId,
},
   new()
     {
        TenantId = tenantId, CompanyId = companyId, CaseId = request.CaseId,
         Title = $"Upload minutes and resolutions — {caseEntity.DebtorName}",
      Category = "Document",
                Deadline = request.MeetingDate.AddDays(3), DeadlineSource = "Manual",
        AssignedToUserId = userId, CreatedByUserId = userId,
            },
  };

    foreach (var task in tasks)
    {
      var summaries = LocalizedSummaryBuilder.BuildTaskSummaryByLanguage(
        task.Title,
        task.Description,
        task.Category,
        task.Deadline,
        task.Status);
      task.Summary = summaries["en"];
      task.SummaryByLanguageJson = JsonSerializer.Serialize(summaries);
    }

    _db.CompanyTasks.AddRange(tasks);

    // 4. Generate meeting notice document via MailMerge (fire-and-forget)
    GeneratedDocument? generatedNotice = null;
    try
    {
      var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();
      generatedNotice = await _mailMerge.GenerateAsync(
                   DocumentTemplateType.CreditorsMeetingMinutes, caseEntity, caseEntity.Company, firm);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to auto-generate meeting notice for case {CaseId}", request.CaseId);
    }

    // 5. Create scheduled email notices for creditor parties
    var emailCount = 0;
    foreach (var party in caseEntity.Parties
 .Where(p => !string.IsNullOrWhiteSpace(p.Email ?? p.Company?.Email)
      && (p.Role is CasePartyRole.SecuredCreditor or CasePartyRole.UnsecuredCreditor
         or CasePartyRole.BudgetaryCreditor or CasePartyRole.EmployeeCreditor)))
    {
      var attachmentsJson = generatedNotice != null
 ? System.Text.Json.JsonSerializer.Serialize(new[] { new { generatedNotice.FileName, generatedNotice.StorageKey } })
 : null;

      _db.ScheduledEmails.Add(new ScheduledEmail
      {
        TenantId = tenantId,
        CaseId = request.CaseId,
        To = party.Email ?? party.Company?.Email ?? "",
        Subject = $"[{caseEntity.CaseNumber}] Creditor Meeting Notice — {request.MeetingDate:dd.MM.yyyy HH:mm}",
        Body = $"You are invited to the creditor meeting for case {caseEntity.CaseNumber} ({caseEntity.DebtorName}).\n\n" +
                  $"Date: {request.MeetingDate:dd.MM.yyyy HH:mm}\n" +
   $"Location: {request.Location ?? "TBD"}\n\n" +
    $"Agenda:\n{request.Agenda ?? "To be circulated."}",
        ScheduledFor = noticeSendDeadline,
        Status = "Scheduled",
        AttachmentsJson = attachmentsJson,
        RelatedPartyIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { party.Id }),
      });
      emailCount++;
    }

    await _db.SaveChangesAsync();

    return new CreditorMeetingResult
    {
      CalendarEventId = calendarEvent.Id,
      TaskCount = tasks.Count,
      EmailCount = emailCount,
      MeetingDate = request.MeetingDate,
      NoticeSendDeadline = noticeSendDeadline,
      NoticeGenerated = generatedNotice != null,
    };
  }
}

public class CreateMeetingRequest
{
  public Guid CaseId { get; set; }
  public DateTime MeetingDate { get; set; }
  public string? Location { get; set; }
  public string? Agenda { get; set; }
  public double? DurationHours { get; set; }
}

public class CreditorMeetingResult
{
  public Guid CalendarEventId { get; set; }
  public int TaskCount { get; set; }
  public int EmailCount { get; set; }
  public DateTime MeetingDate { get; set; }
  public DateTime NoticeSendDeadline { get; set; }
  public bool NoticeGenerated { get; set; }
  public string? Error { get; set; }
}
