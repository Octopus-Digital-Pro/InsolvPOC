using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

public sealed class CaseCalendarService : ICaseCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CaseCalendarService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<CalendarEventDto>> GetEventsAsync(
        Guid caseId, DateTime? from, DateTime? to, string? eventType, CancellationToken ct = default)
    {
        var query = _db.CalendarEvents.Where(e => e.CaseId == caseId);
        if (from.HasValue) query = query.Where(e => e.Start >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Start <= to.Value);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(e => e.EventType == eventType);
        return await query.OrderBy(e => e.Start).Select(e => e.ToDto()).ToListAsync(ct);
    }

    public async Task<List<UnifiedCalendarItem>> GetUnifiedAsync(
        Guid caseId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var items = new List<UnifiedCalendarItem>();

        // Calendar events
        var eventsQuery = _db.CalendarEvents.Where(e => e.CaseId == caseId);
        if (from.HasValue) eventsQuery = eventsQuery.Where(e => e.Start >= from.Value);
        if (to.HasValue) eventsQuery = eventsQuery.Where(e => e.Start <= to.Value);
        var events = await eventsQuery.ToListAsync(ct);
        items.AddRange(events.Select(e => new UnifiedCalendarItem(
            e.Id, e.Title, e.Start, e.End, "Event", e.EventType, false, e.IsCancelled, null)));

        // Task deadlines
        var tasksQuery = _db.CompanyTasks
            .Where(t => t.CaseId == caseId && t.Deadline.HasValue);
        if (from.HasValue) tasksQuery = tasksQuery.Where(t => t.Deadline!.Value >= from.Value);
        if (to.HasValue) tasksQuery = tasksQuery.Where(t => t.Deadline!.Value <= to.Value);
        var tasks = await tasksQuery.ToListAsync(ct);
        items.AddRange(tasks.Select(t => new UnifiedCalendarItem(
            t.Id, t.Title, t.Deadline!.Value, null, "Task", t.Category,
            t.IsCriticalDeadline, false, t.Status.ToString())));

        // Scheduled emails
        var emailsQuery = _db.ScheduledEmails
            .Where(e => e.CaseId == caseId);
        if (from.HasValue) emailsQuery = emailsQuery.Where(e => e.ScheduledFor >= from.Value);
        if (to.HasValue) emailsQuery = emailsQuery.Where(e => e.ScheduledFor <= to.Value);
        var emails = await emailsQuery.ToListAsync(ct);
        items.AddRange(emails.Select(e => new UnifiedCalendarItem(
            e.Id, $"Email: {e.Subject}", e.ScheduledFor, null, "Email", null, false, false, e.Status)));

        return items.OrderBy(i => i.Start).ToList();
    }

    public async Task<CalendarEventDto> CreateAsync(
        Guid caseId, CreateCalendarEventRequest request, CancellationToken ct = default)
    {
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
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Calendar Event Created", "CalendarEvent", calEvent.Id,
            newValues: new { caseId, calEvent.Title, calEvent.Start, calEvent.EventType });

        return calEvent.ToDto();
    }

    public async Task<CalendarEventDto> UpdateAsync(
        Guid caseId, Guid eventId, UpdateCalendarEventRequest request, CancellationToken ct = default)
    {
        var calEvent = await _db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.CaseId == caseId, ct)
            ?? throw new NotFoundException("CalendarEvent", eventId);

        var old = new { calEvent.Title, calEvent.Start };

        if (request.Title != null) calEvent.Title = request.Title;
        if (request.Description != null) calEvent.Description = request.Description;
        if (request.Start.HasValue) calEvent.Start = request.Start.Value;
        if (request.End.HasValue) calEvent.End = request.End.Value;
        if (request.Location != null) calEvent.Location = request.Location;
        if (request.IsCancelled.HasValue) calEvent.IsCancelled = request.IsCancelled.Value;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Calendar Event Updated", "CalendarEvent", calEvent.Id,
            old, new { calEvent.Title, calEvent.Start });

        return calEvent.ToDto();
    }
}
