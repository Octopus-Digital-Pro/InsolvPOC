using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;

namespace Insolvio.API.Controllers;

/// <summary>
/// CRUD for case-level custom deadlines.
/// GET/POST  /api/cases/{caseId}/deadlines
/// PUT/DELETE /api/cases/{caseId}/deadlines/{id}
/// </summary>
[Authorize]
[ApiController]
[Route("api/cases/{caseId:guid}/deadlines")]
public sealed class CaseDeadlinesController : ControllerBase
{
    private readonly ICaseDeadlineService _service;

    public CaseDeadlinesController(ICaseDeadlineService service)
        => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaseDeadlineDto>), 200)]
    public async Task<IActionResult> GetAll(Guid caseId, CancellationToken ct)
    {
        var result = await _service.GetByCaseAsync(caseId, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CaseDeadlineDto), 201)]
    public async Task<IActionResult> Create(Guid caseId, [FromBody] CreateCaseDeadlineBody body, CancellationToken ct)
    {
        var created = await _service.CreateAsync(caseId, body, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CaseDeadlineDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCaseDeadlineBody body, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(caseId, id, body, ct);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid caseId, Guid id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(caseId, id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
