using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// Case-scoped calendar events: deadlines, meetings, scheduled emails, court hearings.
/// Per InsolvencyAppRules: calendar shows deadlines, meetings, scheduled emails.
/// </summary>
[ApiController]
[Route("api/cases/{caseId:guid}/calendar")]
[Authorize]
[RequirePermission(Permission.CaseView)]
public class CaseCalendarController : ControllerBase
{
    private readonly ApplicationDbContext _db;
private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CaseCalendarController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
  _db = db;
  _currentUser = currentUser;
        _audit = audit;
    }

  /// <summary>Get all calendar events for a case, optionally filtered by date range.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCaseCalendarEvents(
        Guid caseId,
  [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? eventType = null)
    {
        var query = _db.CalendarEvents.Where(e => e.CaseId == caseId);

 if (from.HasValue) query = query.Where(e => e.Start >= from.Value);
  if (to.HasValue) query = query.Where(e => e.Start <= to.Value);
    if (!string.IsNullOrWhiteSpace(eventType))
     query = query.Where(e => e.EventType == eventType);

  var events = await query
      .OrderBy(e => e.Start)
   .Select(e => e.ToDto())
.ToListAsync();

     return Ok(events);
    }

    /// <summary>Get a unified calendar view with events, task deadlines, and email schedules.</summary>
    [HttpGet("unified")]
    public async Task<IActionResult> GetUnifiedCalendar(
    Guid caseId,
        [FromQuery] DateTime? from = null,
      [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
 var toDate = to ?? DateTime.UtcNow.AddDays(90);

  // Calendar events
        var events = await _db.CalendarEvents
  .Where(e => e.CaseId == caseId && e.Start >= fromDate && e.Start <= toDate)
     .OrderBy(e => e.Start)
     .Select(e => new UnifiedCalendarItem
    {
   Id = e.Id,
     Title = e.Title,
    Start = e.Start,
     End = e.End,
     Type = "event",
      SubType = e.EventType,
     IsCancelled = e.IsCancelled,
   })
        .ToListAsync();

        // Task deadlines
     var tasks = await _db.CompanyTasks
  .Where(t => t.CaseId == caseId && t.Deadline.HasValue && t.Deadline >= fromDate && t.Deadline <= toDate)
   .OrderBy(t => t.Deadline)
    .Select(t => new UnifiedCalendarItem
   {
  Id = t.Id,
    Title = t.Title,
 Start = t.Deadline!.Value,
     Type = "deadline",
     SubType = t.Category ?? "Task",
   IsCritical = t.IsCriticalDeadline,
  Status = t.Status.ToString(),
            })
  .ToListAsync();

  // Scheduled emails
        var emails = await _db.ScheduledEmails
   .Where(e => e.CaseId == caseId && e.ScheduledFor >= fromDate && e.ScheduledFor <= toDate)
    .OrderBy(e => e.ScheduledFor)
     .Select(e => new UnifiedCalendarItem
     {
           Id = e.Id,
    Title = e.Subject,
Start = e.ScheduledFor,
   Type = "email",
   SubType = e.IsSent ? "Sent" : "Scheduled",
     Status = e.Status,
     })
        .ToListAsync();

        var unified = events.Concat(tasks).Concat(emails).OrderBy(i => i.Start).ToList();
     return Ok(unified);
    }

    /// <summary>Create a calendar event for a case.</summary>
    [HttpPost]
    [RequirePermission(Permission.MeetingCreate)]
    public async Task<IActionResult> CreateCalendarEvent(Guid caseId, [FromBody] CreateCalendarEventRequest request)
    {
        var caseEntity = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId);
     if (caseEntity == null) return NotFound("Case not found");

   var calEvent = new CalendarEvent
     {
       Id = Guid.NewGuid(),
       CaseId = caseId,
  Title = request.Title,
 Description = request.Description,
     Start = request.Start,
       End = request.End,
    AllDay = request.AllDay,
      Location = request.Location,
   EventType = request.EventType,
          ParticipantsJson = request.ParticipantsJson,
  RelatedTaskId = request.RelatedTaskId,
        };

 _db.CalendarEvents.Add(calEvent);
     await _db.SaveChangesAsync();
await _audit.LogAsync("CalendarEvent.Created", calEvent.Id);

    return CreatedAtAction(nameof(GetCaseCalendarEvents), new { caseId }, calEvent.ToDto());
    }

    /// <summary>Update a calendar event.</summary>
    [HttpPut("{eventId:guid}")]
    [RequirePermission(Permission.MeetingCreate)]
    public async Task<IActionResult> UpdateCalendarEvent(Guid caseId, Guid eventId, [FromBody] UpdateCalendarEventRequest request)
    {
        var calEvent = await _db.CalendarEvents
     .FirstOrDefaultAsync(e => e.Id == eventId && e.CaseId == caseId);
    if (calEvent == null) return NotFound();

 if (request.Title != null) calEvent.Title = request.Title;
  if (request.Description != null) calEvent.Description = request.Description;
  if (request.Start.HasValue) calEvent.Start = request.Start.Value;
     if (request.End.HasValue) calEvent.End = request.End.Value;
     if (request.Location != null) calEvent.Location = request.Location;
   if (request.IsCancelled.HasValue) calEvent.IsCancelled = request.IsCancelled.Value;

  await _db.SaveChangesAsync();
        await _audit.LogAsync("CalendarEvent.Updated", calEvent.Id);

  return Ok(calEvent.ToDto());
    }
}

/// <summary>Unified calendar item combining events, task deadlines, and scheduled emails.</summary>
public class UnifiedCalendarItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
public DateTime? End { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? SubType { get; set; }
    public bool IsCritical { get; set; }
    public bool IsCancelled { get; set; }
    public string? Status { get; set; }
}
