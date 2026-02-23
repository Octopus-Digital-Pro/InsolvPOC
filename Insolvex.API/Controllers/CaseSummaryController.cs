using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
[RequirePermission(Permission.SummaryView)]
public class CaseSummaryController : ControllerBase
{
    private readonly ICaseSummaryService _summaryService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CaseSummaryController(ICaseSummaryService summaryService, ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _summaryService = summaryService;
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Generate a new AI summary for a case.</summary>
    [HttpPost("{caseId:guid}/generate")]
    [RequirePermission(Permission.SummaryGenerate)]
    public async Task<IActionResult> Generate(Guid caseId, [FromQuery] string? trigger = "manual")
    {
        var result = await _summaryService.GenerateAsync(caseId);
        if (result.Error != null) return BadRequest(new { message = result.Error });

        var summary = new CaseSummary
        {
            TenantId = _currentUser.TenantId ?? Guid.Empty,
            CaseId = caseId,
            Model = "stub-v1",
            SnapshotJson = result.SnapshotJson,
            Text = result.Text,
            NextActionsJson = System.Text.Json.JsonSerializer.Serialize(result.NextActions),
            RisksJson = System.Text.Json.JsonSerializer.Serialize(result.Risks),
            UpcomingDeadlinesJson = System.Text.Json.JsonSerializer.Serialize(result.UpcomingDeadlines),
            Trigger = trigger,
            GeneratedAt = DateTime.UtcNow,
        };

        _db.CaseSummaries.Add(summary);
        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("CaseSummary.Generated", "CaseSummary", summary.Id,
            newValues: new { caseId, trigger, model = "stub-v1" });

        return Ok(new
        {
            id = summary.Id,
            text = result.Text,
            nextActions = result.NextActions,
            risks = result.Risks,
            upcomingDeadlines = result.UpcomingDeadlines,
            generatedAt = summary.GeneratedAt,
        });
    }

    /// <summary>Get the latest summary for a case.</summary>
    [HttpGet("{caseId:guid}/latest")]
    public async Task<IActionResult> GetLatest(Guid caseId)
    {
        var summary = await _db.CaseSummaries
            .Where(s => s.CaseId == caseId)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefaultAsync();

        if (summary == null)
            return Ok(new { exists = false, message = "No summary generated yet" });

        return Ok(new
        {
            exists = true,
            id = summary.Id,
            text = summary.Text,
            nextActionsJson = summary.NextActionsJson,
            risksJson = summary.RisksJson,
            upcomingDeadlinesJson = summary.UpcomingDeadlinesJson,
            generatedAt = summary.GeneratedAt,
            trigger = summary.Trigger,
            model = summary.Model,
        });
    }

    /// <summary>Get summary history for a case.</summary>
    [HttpGet("{caseId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid caseId, [FromQuery] int take = 10)
    {
        var summaries = await _db.CaseSummaries
            .Where(s => s.CaseId == caseId)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(take)
            .Select(s => new { s.Id, s.GeneratedAt, s.Trigger, s.Model })
            .ToListAsync();

        return Ok(summaries);
    }
}
