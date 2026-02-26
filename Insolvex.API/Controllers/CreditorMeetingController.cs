using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/creditor-meeting")]
[Authorize]
[RequirePermission(Permission.MeetingView)]
public class CreditorMeetingController : ControllerBase
{
    private readonly CreditorMeetingService _meetingService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CreditorMeetingController(CreditorMeetingService meetingService, ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _meetingService = meetingService;
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Call a creditor meeting (sidebar action).</summary>
    [HttpPost]
    [RequirePermission(Permission.MeetingCreate)]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)
            return Unauthorized();

        var result = await _meetingService.CreateMeetingAsync(request, _currentUser.UserId.Value, _currentUser.TenantId.Value);

        if (result.Error != null)
            return BadRequest(new { message = result.Error });

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Meeting.Created",
            EntityType = "CalendarEvent",
            EntityId = result.CalendarEventId,
            NewValues = new { request.CaseId, result.MeetingDate, result.TaskCount, result.NoticeSendDeadline },
            Category = "Meeting",
            Severity = "Info",
        });

        return Ok(new
        {
            calendarEventId = result.CalendarEventId,
            taskCount = result.TaskCount,
            emailCount = result.EmailCount,
            meetingDate = result.MeetingDate,
            noticeSendDeadline = result.NoticeSendDeadline,
            noticeGenerated = result.NoticeGenerated,
            message = "Creditor meeting scheduled. Notice tasks, emails, and documents created.",
        });
    }

    /// <summary>Get calendar events for a case.</summary>
    [HttpGet("calendar/{caseId:guid}")]
    public async Task<IActionResult> GetCaseCalendar(Guid caseId)
    {
        var events = await _db.CalendarEvents
            .Where(e => e.CaseId == caseId && !e.IsCancelled)
            .OrderBy(e => e.Start)
            .Select(e => new
            {
                e.Id, e.Title, e.Description, e.Start, e.End,
                e.AllDay, e.Location, e.EventType, e.IcsUrl,
            })
            .ToListAsync();

        return Ok(events);
    }
}
