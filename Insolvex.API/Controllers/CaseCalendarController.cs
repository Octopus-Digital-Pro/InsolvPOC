using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/calendar")]
[Authorize]
[RequirePermission(Permission.CaseView)]
public class CaseCalendarController : ControllerBase
{
  private readonly ICaseCalendarService _calendar;

  public CaseCalendarController(ICaseCalendarService calendar) => _calendar = calendar;

  [HttpGet]
  public async Task<IActionResult> GetCaseCalendarEvents(
      Guid caseId,
      [FromQuery] DateTime? from = null,
      [FromQuery] DateTime? to = null,
      [FromQuery] string? eventType = null,
      CancellationToken ct = default)
      => Ok(await _calendar.GetEventsAsync(caseId, from, to, eventType, ct));

  [HttpGet("unified")]
  public async Task<IActionResult> GetUnifiedCalendar(
      Guid caseId,
      [FromQuery] DateTime? from = null,
      [FromQuery] DateTime? to = null,
      CancellationToken ct = default)
      => Ok(await _calendar.GetUnifiedAsync(caseId, from, to, ct));

  [HttpPost]
  [RequirePermission(Permission.MeetingCreate)]
  public async Task<IActionResult> CreateCalendarEvent(Guid caseId, [FromBody] CreateCalendarEventRequest request, CancellationToken ct)
      => CreatedAtAction(nameof(GetCaseCalendarEvents), new { caseId },
          await _calendar.CreateAsync(caseId, request, ct));

  [HttpPut("{eventId:guid}")]
  [RequirePermission(Permission.MeetingCreate)]
  public async Task<IActionResult> UpdateCalendarEvent(Guid caseId, Guid eventId, [FromBody] UpdateCalendarEventRequest request, CancellationToken ct)
  {
    var result = await _calendar.UpdateAsync(caseId, eventId, request, ct);
    if (result is null) return NotFound();
    return Ok(result);
  }
}
