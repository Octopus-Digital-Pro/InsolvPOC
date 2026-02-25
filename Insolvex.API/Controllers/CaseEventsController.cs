using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;

namespace Insolvex.API.Controllers;

/// <summary>
/// Exposes the case activity/event timeline.
/// GET /api/cases/{id}/events  – paginated list with optional category filter
/// </summary>
[Authorize]
[ApiController]
[Route("api/cases/{caseId:guid}/events")]
public sealed class CaseEventsController : ControllerBase
{
    private readonly ICaseEventService _caseEvents;

    public CaseEventsController(ICaseEventService caseEvents)
        => _caseEvents = caseEvents;

    /// <summary>
    /// Returns the activity timeline for a case.
    /// </summary>
    /// <param name="caseId">The case ID.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 200 (default 50).</param>
    /// <param name="category">Optional filter: Document | Task | Phase | Deadline | Party | Calendar | Communication | Signing | AI | System</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<CaseEventDto>), 200)]
    public async Task<ActionResult<List<CaseEventDto>>> GetEvents(
        Guid caseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var events = await _caseEvents.GetByCaseAsync(caseId, page, pageSize, category, ct);
        return Ok(events);
    }
}
