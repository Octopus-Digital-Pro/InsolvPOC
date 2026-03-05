using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for case calendar events (create, update, query, unified view).
/// All mutations are audited.
/// </summary>
public interface ICaseCalendarService
{
    Task<List<CalendarEventDto>> GetEventsAsync(
        Guid caseId, DateTime? from, DateTime? to, string? eventType, CancellationToken ct = default);

    Task<List<UnifiedCalendarItem>> GetUnifiedAsync(
        Guid caseId, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<CalendarEventDto> CreateAsync(
        Guid caseId, CreateCalendarEventRequest request, CancellationToken ct = default);

    Task<CalendarEventDto> UpdateAsync(
        Guid caseId, Guid eventId, UpdateCalendarEventRequest request, CancellationToken ct = default);
}
