using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/ai-feedback")]
[Authorize]
public class AiFeedbackController : ControllerBase
{
    private readonly IAiFeedbackService _feedback;

    public AiFeedbackController(IAiFeedbackService feedback) => _feedback = feedback;

    /// <summary>
    /// Record AI correction feedback from annotation modal, case creation, or document review.
    /// </summary>
    [HttpPost("corrections")]
    [RequirePermission(Permission.CaseEdit)]
    public async Task<IActionResult> PostCorrections(
        [FromBody] IReadOnlyList<AiCorrectionFeedbackDto> corrections, CancellationToken ct)
    {
        if (corrections.Count == 0)
            return BadRequest("At least one correction is required.");

        if (corrections.Count > 200)
            return BadRequest("Too many corrections in a single request.");

        await _feedback.RecordCorrectionsAsync(corrections, ct);
        return Ok(new { recorded = corrections.Count });
    }

    /// <summary>
    /// Get aggregated feedback statistics (acceptance rates per field).
    /// </summary>
    [HttpGet("statistics")]
    [RequirePermission(Permission.TrainingView)]
    public async Task<IActionResult> GetStatistics([FromQuery] string? documentType, CancellationToken ct)
    {
        var stats = await _feedback.GetStatisticsAsync(documentType, ct);
        return Ok(stats);
    }
}
