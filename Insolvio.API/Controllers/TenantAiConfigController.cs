using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

/// <summary>
/// Per-tenant AI feature configuration.
/// GET  /api/settings/tenant-ai-config              → current tenant's config (TenantAdmin+)
/// GET  /api/settings/tenant-ai-config/{tenantId}   → specific tenant (GlobalAdmin only)
/// PUT  /api/settings/tenant-ai-config/{tenantId}   → update (GlobalAdmin only)
/// </summary>
[ApiController]
[Route("api/settings/tenant-ai-config")]
[Authorize]
public sealed class TenantAiConfigController : ControllerBase
{
    private readonly ITenantAiConfigService _service;
    private readonly ICurrentUserService _currentUser;

    public TenantAiConfigController(ITenantAiConfigService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Get AI config for the current user's tenant.</summary>
    [HttpGet]
    [RequirePermission(Permission.TenantAiConfigView)]
    [ProducesResponseType(typeof(TenantAiConfigDto), 200)]
    public async Task<IActionResult> GetOwn(CancellationToken ct)
        => Ok(await _service.GetAsync(null, ct));

    /// <summary>Get AI config for a specific tenant (GlobalAdmin).</summary>
    [HttpGet("{tenantId:guid}")]
    [RequirePermission(Permission.TenantAiConfigView)]
    [ProducesResponseType(typeof(TenantAiConfigDto), 200)]
    public async Task<IActionResult> GetForTenant(Guid tenantId, CancellationToken ct)
        => Ok(await _service.GetAsync(tenantId, ct));

    /// <summary>Update API key / model for the current user's own tenant (TenantAdmin self-service).</summary>
    [HttpPut("own-key")]
    [RequirePermission(Permission.TenantAiKeyEdit)]
    [ProducesResponseType(typeof(TenantAiConfigDto), 200)]
    public async Task<IActionResult> UpdateOwnKey(
        [FromBody] UpdateTenantAiKeyRequest request, CancellationToken ct)
        => Ok(await _service.UpdateOwnApiKeyAsync(request, ct));

    /// <summary>Update AI config for a tenant (GlobalAdmin only).</summary>
    [HttpPut("{tenantId:guid}")]
    [RequirePermission(Permission.TenantAiConfigEdit)]
    [ProducesResponseType(typeof(TenantAiConfigDto), 200)]
    public async Task<IActionResult> Update(Guid tenantId,
        [FromBody] UpdateTenantAiConfigRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(tenantId, request, ct));
}
