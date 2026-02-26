using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// CRUD for workflow stage definitions.
/// Global stages (TenantId = null) are seeded by the system.
/// Tenants can override by creating a record with the same StageKey.
/// Resolution: tenant-specific → global fallback.
/// </summary>
[ApiController]
[Route("api/workflow-stages")]
[Authorize]
public class WorkflowStagesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public WorkflowStagesController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ── List (resolved: tenant override → global fallback) ───────────────────

    /// <summary>
    /// Returns the effective workflow stages for the current tenant.
    /// For each StageKey, returns the tenant override if it exists, else the global definition.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        // Load all global + tenant stages
        var all = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == null || s.TenantId == tenantId)
            .Include(s => s.Templates).ThenInclude(t => t.DocumentTemplate)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        // Resolve: tenant override wins over global
        var resolved = all
            .GroupBy(s => s.StageKey)
            .Select(g => g.FirstOrDefault(s => s.TenantId == tenantId) ?? g.First(s => s.TenantId == null))
            .OrderBy(s => s.SortOrder)
            .Select(s => new WorkflowStageDto(
                s.Id, s.TenantId, s.StageKey, s.Name, s.Description,
                s.SortOrder, s.ApplicableProcedureTypes, s.IsActive,
                s.Templates.Count, s.CreatedOn, s.LastModifiedOn))
            .ToList();

        return Ok(resolved);
    }

    // ── List global only (admin view) ────────────────────────────────────────

    [HttpGet("global")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> GetGlobal(CancellationToken ct)
    {
        var stages = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == null)
            .Include(s => s.Templates).ThenInclude(t => t.DocumentTemplate)
            .OrderBy(s => s.SortOrder)
            .Select(s => new WorkflowStageDto(
                s.Id, s.TenantId, s.StageKey, s.Name, s.Description,
                s.SortOrder, s.ApplicableProcedureTypes, s.IsActive,
                s.Templates.Count, s.CreatedOn, s.LastModifiedOn))
            .ToListAsync(ct);

        return Ok(stages);
    }

    // ── Get single (detail with JSON configs) ────────────────────────────────

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var s = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Include(s => s.Templates).ThenInclude(t => t.DocumentTemplate)
            .FirstOrDefaultAsync(s => s.Id == id && (s.TenantId == null || s.TenantId == tenantId), ct);

        if (s == null) return NotFound();

        return Ok(ToDetailDto(s));
    }

    // ── Create / Update global stage ─────────────────────────────────────────

    [HttpPost("global")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpsertGlobal([FromBody] UpsertWorkflowStageCommand cmd, CancellationToken ct)
    {
        var existing = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Include(s => s.Templates)
            .FirstOrDefaultAsync(s => s.TenantId == null && s.StageKey == cmd.StageKey, ct);

        if (existing != null)
        {
            ApplyCommand(existing, cmd);
            await SyncTemplates(existing, cmd.Templates, ct);
            await _db.SaveChangesAsync(ct);
            return Ok(ToDetailDto(existing));
        }

        var stage = new WorkflowStageDefinition { TenantId = null };
        ApplyCommand(stage, cmd);
        _db.WorkflowStageDefinitions.Add(stage);
        await _db.SaveChangesAsync(ct);
        await SyncTemplates(stage, cmd.Templates, ct);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = stage.Id }, ToDetailDto(stage));
    }

    // ── Create / Update tenant override ──────────────────────────────────────

    [HttpPost("override")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpsertTenantOverride([FromBody] UpsertWorkflowStageCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        var existing = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Include(s => s.Templates)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.StageKey == cmd.StageKey, ct);

        if (existing != null)
        {
            ApplyCommand(existing, cmd);
            await SyncTemplates(existing, cmd.Templates, ct);
            await _db.SaveChangesAsync(ct);
            return Ok(ToDetailDto(existing));
        }

        var stage = new WorkflowStageDefinition { TenantId = tenantId };
        ApplyCommand(stage, cmd);
        _db.WorkflowStageDefinitions.Add(stage);
        await _db.SaveChangesAsync(ct);
        await SyncTemplates(stage, cmd.Templates, ct);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = stage.Id }, ToDetailDto(stage));
    }

    // ── Delete tenant override (revert to global) ────────────────────────────

    [HttpDelete("override/{stageKey}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> DeleteTenantOverride(string stageKey, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context required.");

        var existing = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.StageKey == stageKey, ct);

        if (existing == null) return NotFound();

        _db.WorkflowStageDefinitions.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Tenant override removed. Global definition will be used." });
    }

    // ── Delete global stage ──────────────────────────────────────────────────

    [HttpDelete("global/{stageKey}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> DeleteGlobal(string stageKey, CancellationToken ct)
    {
        var existing = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == null && s.StageKey == stageKey, ct);

        if (existing == null) return NotFound();

        _db.WorkflowStageDefinitions.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Global stage definition deleted." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ApplyCommand(WorkflowStageDefinition stage, UpsertWorkflowStageCommand cmd)
    {
        stage.StageKey = cmd.StageKey.Trim().ToLowerInvariant();
        stage.Name = cmd.Name.Trim();
        stage.Description = cmd.Description?.Trim();
        stage.SortOrder = cmd.SortOrder;
        stage.ApplicableProcedureTypes = cmd.ApplicableProcedureTypes?.Trim();
        stage.RequiredFieldsJson = cmd.RequiredFieldsJson;
        stage.RequiredPartyRolesJson = cmd.RequiredPartyRolesJson;
        stage.RequiredDocTypesJson = cmd.RequiredDocTypesJson;
        stage.RequiredTaskTemplatesJson = cmd.RequiredTaskTemplatesJson;
        stage.ValidationRulesJson = cmd.ValidationRulesJson;
        stage.OutputDocTypesJson = cmd.OutputDocTypesJson;
        stage.OutputTasksJson = cmd.OutputTasksJson;
        stage.AllowedTransitionsJson = cmd.AllowedTransitionsJson;
        stage.IsActive = cmd.IsActive;
    }

    private async Task SyncTemplates(WorkflowStageDefinition stage, List<UpsertStageTemplateItem>? items, CancellationToken ct)
    {
        // Remove existing
        var existing = await _db.WorkflowStageTemplates
            .Where(t => t.StageDefinitionId == stage.Id)
            .ToListAsync(ct);
        _db.WorkflowStageTemplates.RemoveRange(existing);

        if (items == null || items.Count == 0) return;

        foreach (var item in items)
        {
            _db.WorkflowStageTemplates.Add(new WorkflowStageTemplate
            {
                StageDefinitionId = stage.Id,
                DocumentTemplateId = item.DocumentTemplateId,
                IsRequired = item.IsRequired,
                SortOrder = item.SortOrder,
                Notes = item.Notes,
            });
        }
    }

    private static WorkflowStageDetailDto ToDetailDto(WorkflowStageDefinition s)
    {
        return new WorkflowStageDetailDto(
            s.Id, s.TenantId, s.StageKey, s.Name, s.Description,
            s.SortOrder, s.ApplicableProcedureTypes,
            s.RequiredFieldsJson, s.RequiredPartyRolesJson,
            s.RequiredDocTypesJson, s.RequiredTaskTemplatesJson,
            s.ValidationRulesJson, s.OutputDocTypesJson,
            s.OutputTasksJson, s.AllowedTransitionsJson,
            s.IsActive,
            s.Templates.OrderBy(t => t.SortOrder).Select(t => new WorkflowStageTemplateDto(
                t.Id, t.DocumentTemplateId,
                t.DocumentTemplate?.Name ?? "",
                t.DocumentTemplate?.TemplateType.ToString(),
                t.IsRequired, t.SortOrder, t.Notes)).ToList(),
            s.CreatedOn, s.LastModifiedOn);
    }
}
