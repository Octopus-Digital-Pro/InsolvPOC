using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

/// <summary>
/// AI-powered case summary and chat assistant.
///
/// POST /api/cases/{caseId}/ai/summary         → Generate + save AI summary
/// POST /api/cases/{caseId}/ai/chat            → Send a chat message
/// GET  /api/cases/{caseId}/ai/chat            → Get chat history
/// DELETE /api/cases/{caseId}/ai/chat          → Clear chat history
/// GET  /api/cases/{caseId}/ai/enabled         → Check if AI is enabled for this case's tenant
/// </summary>
[ApiController]
[Route("api/cases/{caseId:guid}/ai")]
[Authorize]
public sealed class CaseAiController : ControllerBase
{
    private readonly ICaseAiService _caseAi;
    private readonly ITenantAiConfigService _tenantAiConfig;
    private readonly ICurrentUserService _currentUser;

    public CaseAiController(ICaseAiService caseAi, ITenantAiConfigService tenantAiConfig, ICurrentUserService currentUser)
    {
        _caseAi = caseAi;
        _tenantAiConfig = tenantAiConfig;
        _currentUser = currentUser;
    }

    /// <summary>Check whether AI features are enabled for the current tenant.</summary>
    [HttpGet("enabled")]
    [RequirePermission(Permission.SummaryView)]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> CheckEnabled(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        var cfg = await _tenantAiConfig.GetAsync(tenantId, ct);
        return Ok(new
        {
            aiEnabled = cfg.AiEnabled,
            summaryEnabled = cfg.AiEnabled && cfg.SummaryEnabled,
            chatEnabled = cfg.AiEnabled && cfg.ChatEnabled,
            usagePercent = cfg.MonthlyTokenLimit > 0
                ? (double)cfg.CurrentMonthTokensUsed / cfg.MonthlyTokenLimit * 100
                : 0.0,
            atLimit = cfg.MonthlyTokenLimit > 0 && cfg.CurrentMonthTokensUsed >= cfg.MonthlyTokenLimit,
        });
    }

    /// <summary>Get the latest saved AI summary for a case (null if none generated yet).</summary>
    [HttpGet("summary")]
    [RequirePermission(Permission.SummaryView)]
    [ProducesResponseType(typeof(CaseSummaryDto), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> GetSummary(Guid caseId, CancellationToken ct)
    {
        var dto = await _caseAi.GetLatestSummaryAsync(caseId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>Generate an AI-powered case summary and persist it.</summary>
    [HttpPost("summary")]
    [RequirePermission(Permission.SummaryGenerate)]
    [ProducesResponseType(typeof(CaseSummaryDto), 200)]
    public async Task<IActionResult> GenerateSummary(
        Guid caseId,
        [FromBody] AiSummaryRequest? request,
        CancellationToken ct)
    {
        var language = request?.Language ?? "ro";
        var dto = await _caseAi.GenerateSummaryAsync(caseId, language, ct);
        return Ok(dto);
    }

    /// <summary>Send a user message to the AI assistant.</summary>
    [HttpPost("chat")]
    [RequirePermission(Permission.AiChatUse)]
    [ProducesResponseType(typeof(AiChatResponse), 200)]
    public async Task<IActionResult> Chat(
        Guid caseId,
        [FromBody] AiChatRequest request,
        CancellationToken ct)
    {
        try
        {
            var response = await _caseAi.ChatAsync(caseId, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get chat history for a case.</summary>
    [HttpGet("chat")]
    [RequirePermission(Permission.AiChatUse)]
    [ProducesResponseType(typeof(List<AiChatMessageDto>), 200)]
    public async Task<IActionResult> GetChatHistory(
        Guid caseId,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
        => Ok(await _caseAi.GetChatHistoryAsync(caseId, take, ct));

    /// <summary>Clear all chat history for a case.</summary>
    [HttpDelete("chat")]
    [RequirePermission(Permission.AiChatUse)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ClearChatHistory(Guid caseId, CancellationToken ct)
    {
        await _caseAi.ClearChatHistoryAsync(caseId, ct);
        return NoContent();
    }
}
