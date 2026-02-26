using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;

namespace Insolvex.Data.Services;

public sealed class CaseWorkflowService : ICaseWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CaseWorkflowService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ── Public methods ──────────────────────────────────────────────────

    public async Task<List<CaseWorkflowStageDto>> GetStagesAsync(Guid caseId, CancellationToken ct)
    {
        var stages = await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        if (stages.Count == 0)
        {
            stages = await InitializeStagesAsync(caseId, ct);
        }

        // Run validation for all stages
        var caseData = await LoadCaseDataAsync(caseId, ct);
        var result = new List<CaseWorkflowStageDto>();

        foreach (var stage in stages)
        {
            var validation = ValidateStageRequirements(stage.StageDefinition, caseData);
            result.Add(ToDto(stage, validation));
        }

        return result;
    }

    public async Task<ValidationResultDto> ValidateStageAsync(Guid caseId, string stageKey, CancellationToken ct)
    {
        var stage = await GetStageOrThrowAsync(caseId, stageKey, ct);
        var caseData = await LoadCaseDataAsync(caseId, ct);
        return ValidateStageRequirements(stage.StageDefinition, caseData);
    }

    public async Task<CaseWorkflowStageDto> StartStageAsync(Guid caseId, string stageKey, CancellationToken ct)
    {
        var stages = await EnsureStagesAsync(caseId, ct);
        var stage = stages.FirstOrDefault(s => s.StageKey == stageKey)
            ?? throw new BusinessException($"Stage '{stageKey}' not found for case {caseId}.");

        if (stage.Status == CaseWorkflowStatus.InProgress)
            return ToDto(stage, null);

        if (stage.Status == CaseWorkflowStatus.Completed)
            throw new BusinessException($"Stage '{stageKey}' is already completed. Reopen it first.");

        // Gate: all previous stages (lower SortOrder) must be Completed or Skipped
        var blockers = stages
            .Where(s => s.SortOrder < stage.SortOrder
                     && s.Status != CaseWorkflowStatus.Completed
                     && s.Status != CaseWorkflowStatus.Skipped)
            .Select(s => s.StageDefinition?.Name ?? s.StageKey)
            .ToList();

        if (blockers.Count > 0)
            throw new BusinessException($"Cannot start '{stage.StageDefinition?.Name ?? stageKey}'. Pending stages: {string.Join(", ", blockers)}");

        stage.Status = CaseWorkflowStatus.InProgress;
        stage.StartedAt = DateTime.UtcNow;
        stage.LastModifiedOn = DateTime.UtcNow;
        stage.LastModifiedBy = _currentUser.UserId?.ToString();
        await _db.SaveChangesAsync(ct);

        var caseData = await LoadCaseDataAsync(caseId, ct);
        return ToDto(stage, ValidateStageRequirements(stage.StageDefinition, caseData));
    }

    public async Task<CaseWorkflowStageDto> CompleteStageAsync(Guid caseId, string stageKey, CancellationToken ct)
    {
        var stage = await GetStageOrThrowAsync(caseId, stageKey, ct);

        if (stage.Status == CaseWorkflowStatus.Completed)
            return ToDto(stage, null);

        if (stage.Status == CaseWorkflowStatus.NotStarted)
            throw new BusinessException($"Stage '{stageKey}' has not been started yet.");

        // Validate requirements
        var caseData = await LoadCaseDataAsync(caseId, ct);
        var validation = ValidateStageRequirements(stage.StageDefinition, caseData);

        if (!validation.CanComplete)
            throw new BusinessException($"Cannot complete stage '{stage.StageDefinition?.Name ?? stageKey}'. Missing requirements: {string.Join("; ", validation.Messages)}");

        stage.Status = CaseWorkflowStatus.Completed;
        stage.CompletedAt = DateTime.UtcNow;
        stage.CompletedBy = _currentUser.Email ?? _currentUser.UserId?.ToString();
        stage.ValidationResultJson = JsonSerializer.Serialize(validation, _jsonOpts);
        stage.LastModifiedOn = DateTime.UtcNow;
        stage.LastModifiedBy = _currentUser.UserId?.ToString();
        await _db.SaveChangesAsync(ct);

        return ToDto(stage, validation);
    }

    public async Task<CaseWorkflowStageDto> SkipStageAsync(Guid caseId, string stageKey, string? reason, CancellationToken ct)
    {
        var stage = await GetStageOrThrowAsync(caseId, stageKey, ct);

        if (stage.Status == CaseWorkflowStatus.Completed)
            throw new BusinessException("Cannot skip a completed stage. Reopen it first.");

        stage.Status = CaseWorkflowStatus.Skipped;
        stage.CompletedAt = DateTime.UtcNow;
        stage.CompletedBy = _currentUser.Email ?? _currentUser.UserId?.ToString();
        stage.Notes = reason;
        stage.LastModifiedOn = DateTime.UtcNow;
        stage.LastModifiedBy = _currentUser.UserId?.ToString();
        await _db.SaveChangesAsync(ct);

        return ToDto(stage, null);
    }

    public async Task<CaseWorkflowStageDto> ReopenStageAsync(Guid caseId, string stageKey, CancellationToken ct)
    {
        var stage = await GetStageOrThrowAsync(caseId, stageKey, ct);

        if (stage.Status != CaseWorkflowStatus.Completed && stage.Status != CaseWorkflowStatus.Skipped)
            throw new BusinessException("Can only reopen a completed or skipped stage.");

        stage.Status = CaseWorkflowStatus.InProgress;
        stage.CompletedAt = null;
        stage.CompletedBy = null;
        stage.StartedAt ??= DateTime.UtcNow;
        stage.LastModifiedOn = DateTime.UtcNow;
        stage.LastModifiedBy = _currentUser.UserId?.ToString();
        await _db.SaveChangesAsync(ct);

        var caseData = await LoadCaseDataAsync(caseId, ct);
        return ToDto(stage, ValidateStageRequirements(stage.StageDefinition, caseData));
    }

    // ── Internal helpers ────────────────────────────────────────────────

    /// <summary>
    /// Initialize workflow stages for a case by resolving stage definitions
    /// (tenant override → global fallback) and creating CaseWorkflowStage rows.
    /// </summary>
    private async Task<List<CaseWorkflowStage>> InitializeStagesAsync(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        // Load all definitions: globals + tenant overrides
        var allDefs = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == null || d.TenantId == tenantId)
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        // Resolve: tenant-specific overrides global
        var resolved = allDefs
            .GroupBy(d => d.StageKey)
            .Select(g => g.FirstOrDefault(d => d.TenantId == tenantId) ?? g.First(d => d.TenantId == null))
            .OrderBy(d => d.SortOrder)
            .ToList();

        var stages = new List<CaseWorkflowStage>();
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId?.ToString();

        foreach (var def in resolved)
        {
            var stage = new CaseWorkflowStage
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                StageDefinitionId = def.Id,
                StageKey = def.StageKey,
                SortOrder = def.SortOrder,
                Status = CaseWorkflowStatus.NotStarted,
                CreatedOn = now,
                CreatedBy = userId,
                LastModifiedOn = now,
                LastModifiedBy = userId,
            };

            stages.Add(stage);
            _db.CaseWorkflowStages.Add(stage);
        }

        // Auto-start the first stage
        if (stages.Count > 0)
        {
            stages[0].Status = CaseWorkflowStatus.InProgress;
            stages[0].StartedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        // Reload with StageDefinition navigation
        return await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    private async Task<List<CaseWorkflowStage>> EnsureStagesAsync(Guid caseId, CancellationToken ct)
    {
        var stages = await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        if (stages.Count == 0)
            stages = await InitializeStagesAsync(caseId, ct);

        return stages;
    }

    private async Task<CaseWorkflowStage> GetStageOrThrowAsync(Guid caseId, string stageKey, CancellationToken ct)
    {
        var stages = await EnsureStagesAsync(caseId, ct);
        return stages.FirstOrDefault(s => s.StageKey == stageKey)
            ?? throw new BusinessException($"Stage '{stageKey}' not found for case {caseId}.");
    }

    // ── Validation engine ───────────────────────────────────────────────

    private record CaseValidationData(
        InsolvencyCase Case,
        List<string> PartyRoles,
        List<string> DocTypes,
        List<string> CompletedTaskKeys);

    private async Task<CaseValidationData> LoadCaseDataAsync(Guid caseId, CancellationToken ct)
    {
        var insolvencyCase = await _db.InsolvencyCases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId, ct)
            ?? throw new BusinessException($"Case {caseId} not found.");

        var partyRoles = await _db.CaseParties
            .Where(p => p.CaseId == caseId)
            .Select(p => p.Role.ToString())
            .Distinct()
            .ToListAsync(ct);

        var docTypes = await _db.InsolvencyDocuments
            .Where(d => d.CaseId == caseId)
            .Select(d => d.DocType)
            .Distinct()
            .ToListAsync(ct);

        var completedTaskKeys = await _db.CompanyTasks
            .Where(t => t.CaseId == caseId && t.Status == Insolvex.Domain.Enums.TaskStatus.Done)
            .Select(t => t.Title)
            .Distinct()
            .ToListAsync(ct);

        return new CaseValidationData(insolvencyCase, partyRoles, docTypes, completedTaskKeys);
    }

    private static ValidationResultDto ValidateStageRequirements(
        WorkflowStageDefinition? def,
        CaseValidationData data)
    {
        if (def is null)
            return new ValidationResultDto(true, [], [], [], [], []);

        var missingFields = new List<string>();
        var missingRoles = new List<string>();
        var missingDocs = new List<string>();
        var missingTasks = new List<string>();
        var messages = new List<string>();

        // Check required fields on the case entity
        if (!string.IsNullOrWhiteSpace(def.RequiredFieldsJson))
        {
            var fields = Deserialize<List<string>>(def.RequiredFieldsJson);
            foreach (var field in fields)
            {
                var prop = typeof(InsolvencyCase).GetProperty(field);
                if (prop is null) continue;
                var val = prop.GetValue(data.Case);
                var isEmpty = val is null || (val is string s && string.IsNullOrWhiteSpace(s));
                if (isEmpty)
                {
                    missingFields.Add(field);
                    messages.Add($"Missing field: {field}");
                }
            }
        }

        // Check required party roles
        if (!string.IsNullOrWhiteSpace(def.RequiredPartyRolesJson))
        {
            var roles = Deserialize<List<string>>(def.RequiredPartyRolesJson);
            foreach (var role in roles)
            {
                if (!data.PartyRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    missingRoles.Add(role);
                    messages.Add($"Missing party role: {role}");
                }
            }
        }

        // Check required document types
        if (!string.IsNullOrWhiteSpace(def.RequiredDocTypesJson))
        {
            var docTypes = Deserialize<List<string>>(def.RequiredDocTypesJson);
            foreach (var dt in docTypes)
            {
                if (!data.DocTypes.Contains(dt, StringComparer.OrdinalIgnoreCase))
                {
                    missingDocs.Add(dt);
                    messages.Add($"Missing document: {dt}");
                }
            }
        }

        // Check required task templates
        if (!string.IsNullOrWhiteSpace(def.RequiredTaskTemplatesJson))
        {
            var taskKeys = Deserialize<List<string>>(def.RequiredTaskTemplatesJson);
            foreach (var tk in taskKeys)
            {
                if (!data.CompletedTaskKeys.Contains(tk, StringComparer.OrdinalIgnoreCase))
                {
                    missingTasks.Add(tk);
                    messages.Add($"Incomplete task: {tk}");
                }
            }
        }

        var canComplete = missingFields.Count == 0
                       && missingRoles.Count == 0
                       && missingDocs.Count == 0
                       && missingTasks.Count == 0;

        return new ValidationResultDto(canComplete, missingFields, missingRoles, missingDocs, missingTasks, messages);
    }

    // ── Mapping ─────────────────────────────────────────────────────────

    private static CaseWorkflowStageDto ToDto(CaseWorkflowStage stage, ValidationResultDto? validation)
    {
        return new CaseWorkflowStageDto(
            stage.Id,
            stage.CaseId,
            stage.StageDefinitionId,
            stage.StageKey,
            stage.StageDefinition?.Name ?? stage.StageKey,
            stage.StageDefinition?.Description,
            stage.SortOrder,
            stage.Status.ToString(),
            stage.StartedAt,
            stage.CompletedAt,
            stage.CompletedBy,
            validation
        );
    }

    private static T Deserialize<T>(string json) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(json, _jsonOpts) ?? new T(); }
        catch { return new T(); }
    }
}
