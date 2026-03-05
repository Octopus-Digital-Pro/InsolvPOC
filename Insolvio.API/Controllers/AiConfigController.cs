using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

/// <summary>
/// System-level AI configuration — GlobalAdmin only.
/// GET  /api/settings/ai-config   → returns current config (key masked)
/// PUT  /api/settings/ai-config   → create or update config
/// </summary>
[ApiController]
[Route("api/settings/ai-config")]
[Authorize]
public sealed class AiConfigController : ControllerBase
{
    private readonly IAiConfigService _aiConfig;

    public AiConfigController(IAiConfigService aiConfig)
        => _aiConfig = aiConfig;

    [HttpGet]
    [RequirePermission(Permission.SystemConfigView)]
    [ProducesResponseType(typeof(AiConfigDto), 200)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _aiConfig.GetAsync(ct));

    [HttpPut]
    [RequirePermission(Permission.SystemConfigEdit)]
    [ProducesResponseType(typeof(AiConfigDto), 200)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateAiConfigRequest request, CancellationToken ct)
        => Ok(await _aiConfig.UpdateAsync(request, ct));
}
