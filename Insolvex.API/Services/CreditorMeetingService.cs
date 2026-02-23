using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Orchestrates the "Call creditor meeting" sidebar action.
/// Creates calendar event, generates notice tasks, schedules email sends.
/// </summary>
public class CreditorMeetingService
{
 private readonly ApplicationDbContext _db;
    private readonly DeadlineEngine _deadlineEngine;

    public CreditorMeetingService(ApplicationDbContext db, DeadlineEngine deadlineEngine)
    {
        _db = db;
        _deadlineEngine = deadlineEngine;
    }

    public async Task<CreditorMeetingResult> CreateMeetingAsync(CreateMeetingRequest request, Guid userId, Guid tenantId)
    {
     var caseEntity = await _db.InsolvencyCases
      .Include(c => c.Parties).ThenInclude(p => p.Company)
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
var company = caseEntity.CompanyId.HasValue
            ? await _db.Companies.FindAsync(caseEntity.CompanyId.Value)
 : null;
        var companyId = company?.Id ?? caseEntity.Parties.FirstOrDefault()?.CompanyId ?? Guid.Empty;

        var tasks = new List<CompanyTask>
{
 new()
            {
   TenantId = tenantId,
       CompanyId = companyId,
     CaseId = request.CaseId,
   Title = "Generate meeting notice pack",
           Category = "Meeting",
  Stage = CaseStage.CreditorMeeting,
  Deadline = noticeSendDeadline,
    DeadlineSource = "CompanyDefault",
  IsCriticalDeadline = true,
 AssignedToUserId = userId,
       CreatedByUserId = userId,
       },
        new()
  {
          TenantId = tenantId,
     CompanyId = companyId,
          CaseId = request.CaseId,
   Title = "Send meeting invites/notices",
    Category = "Email",
     Stage = CaseStage.CreditorMeeting,
       Deadline = noticeSendDeadline,
       DeadlineSource = "CompanyDefault",
       IsCriticalDeadline = true,
                AssignedToUserId = userId,
           CreatedByUserId = userId,
  },
            new()
   {
         TenantId = tenantId,
      CompanyId = companyId,
    CaseId = request.CaseId,
      Title = "Prepare voting register",
  Category = "Document",
      Stage = CaseStage.CreditorMeeting,
   Deadline = request.MeetingDate.AddDays(-1),
     DeadlineSource = "CompanyDefault",
   AssignedToUserId = userId,
        CreatedByUserId = userId,
   },
 new()
    {
    TenantId = tenantId,
      CompanyId = companyId,
     CaseId = request.CaseId,
    Title = "Record attendance and votes",
         Category = "Meeting",
       Stage = CaseStage.CreditorMeeting,
    Deadline = request.MeetingDate.AddDays(1),
      DeadlineSource = "Manual",
        AssignedToUserId = userId,
                CreatedByUserId = userId,
    },
            new()
    {
      TenantId = tenantId,
   CompanyId = companyId,
          CaseId = request.CaseId,
         Title = "Upload minutes and resolutions",
   Category = "Document",
 Stage = CaseStage.CreditorMeeting,
 Deadline = request.MeetingDate.AddDays(3),
            DeadlineSource = "Manual",
      AssignedToUserId = userId,
  CreatedByUserId = userId,
  },
     };

        _db.CompanyTasks.AddRange(tasks);

        // 4. Create scheduled email notices for creditor parties
        foreach (var party in caseEntity.Parties
          .Where(p => !string.IsNullOrWhiteSpace(p.Email ?? p.Company?.Email)
              && (p.Role is CasePartyRole.SecuredCreditor or CasePartyRole.UnsecuredCreditor
  or CasePartyRole.BudgetaryCreditor or CasePartyRole.EmployeeCreditor)))
        {
       _db.ScheduledEmails.Add(new ScheduledEmail
         {
             TenantId = tenantId,
     To = party.Email ?? party.Company?.Email ?? "",
    Subject = $"[{caseEntity.CaseNumber}] Creditor Meeting Notice — {request.MeetingDate:dd.MM.yyyy HH:mm}",
         Body = $"You are invited to the creditor meeting for case {caseEntity.CaseNumber} ({caseEntity.DebtorName}).\n\nDate: {request.MeetingDate:dd.MM.yyyy HH:mm}\nLocation: {request.Location ?? "TBD"}\n\nAgenda:\n{request.Agenda ?? "To be circulated."}",
   ScheduledFor = noticeSendDeadline,
      });
        }

  await _db.SaveChangesAsync();

        return new CreditorMeetingResult
     {
   CalendarEventId = calendarEvent.Id,
    TaskCount = tasks.Count,
     MeetingDate = request.MeetingDate,
  NoticeSendDeadline = noticeSendDeadline,
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
    public DateTime MeetingDate { get; set; }
    public DateTime NoticeSendDeadline { get; set; }
    public string? Error { get; set; }
}
