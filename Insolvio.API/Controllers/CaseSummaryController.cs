using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
[RequirePermission(Permission.SummaryView)]
public class CaseSummaryController : ControllerBase
{
    private readonly ICaseSummaryService _summary;

    public CaseSummaryController(ICaseSummaryService summary) => _summary = summary;

    /// <summary>Generate a new AI summary and persist it.</summary>
    [HttpPost("{caseId:guid}/generate")]
    [RequirePermission(Permission.SummaryGenerate)]
    public async Task<IActionResult> Generate(Guid caseId, [FromQuery] string? trigger = "manual")
    {
        var dto = await _summary.GenerateAndSaveAsync(caseId, trigger);
        return Ok(dto);
    }

    /// <summary>Get the latest summary for a case.</summary>
    [HttpGet("{caseId:guid}/latest")]
    public async Task<IActionResult> GetLatest(Guid caseId)
    {
        var dto = await _summary.GetLatestAsync(caseId);
        if (dto is null) return Ok(new { exists = false, message = "No summary generated yet" });
        return Ok(new { exists = true, data = dto });
    }

    /// <summary>Get summary history for a case.</summary>
    [HttpGet("{caseId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid caseId, [FromQuery] int take = 10)
        => Ok(await _summary.GetHistoryAsync(caseId, take));
}
