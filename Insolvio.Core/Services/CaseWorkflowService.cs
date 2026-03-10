using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Services;

public sealed class CaseWorkflowService : ICaseWorkflowService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CaseWorkflowService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
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

    public async Task<CaseWorkflowStageDto> StartStageAsync(Guid caseId, string stageKey, bool acknowledgeWarnings = false, CancellationToken ct = default)
    {
        var stages = await EnsureStagesAsync(caseId, ct);
        var stage = stages.FirstOrDefault(s => s.StageKey == stageKey)
            ?? throw new BusinessException($"Stage '{stageKey}' not found for case {caseId}.");

        if (stage.Status == CaseWorkflowStatus.InProgress)
            return ToDto(stage, null);

        if (stage.Status == CaseWorkflowStatus.Completed)
            throw new BusinessException($"Stage '{stageKey}' is already completed. Reopen it first.");

        // Gate: all previous stages (lower SortOrder) must be Completed or Skipped
        // If the user has acknowledged the warning, allow bypassing this gate.
        var blockers = stages
            .Where(s => s.SortOrder < stage.SortOrder
                     && s.Status != CaseWorkflowStatus.Completed
                     && s.Status != CaseWorkflowStatus.Skipped)
            .Select(s => s.StageDefinition?.Name ?? s.StageKey)
            .ToList();

        if (blockers.Count > 0 && !acknowledgeWarnings)
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
    /// Safe against concurrent calls: if another request wins the race and inserts
    /// the same rows first, we discard our pending inserts and reload from the DB.
    /// </summary>
    private async Task<List<CaseWorkflowStage>> InitializeStagesAsync(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        // Resolve the case's ProcedureType so we only create stages relevant to it
        var procedureType = await _db.InsolvencyCases
            .Where(c => c.Id == caseId)
            .Select(c => c.ProcedureType)
            .FirstOrDefaultAsync(ct);
        var procedureTypeStr = procedureType.ToString();

        // Load all definitions: globals + tenant overrides
        var allDefs = await _db.WorkflowStageDefinitions
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == null || d.TenantId == tenantId)
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        // Resolve: tenant-specific overrides global, then filter to this procedure type
        var resolved = allDefs
            .GroupBy(d => d.StageKey)
            .Select(g => g.FirstOrDefault(d => d.TenantId == tenantId) ?? g.First(d => d.TenantId == null))
            .Where(d => string.IsNullOrWhiteSpace(d.ApplicableProcedureTypes) ||
                        d.ApplicableProcedureTypes
                            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Contains(procedureTypeStr, StringComparer.OrdinalIgnoreCase))
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

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyOrDeadlockException(ex))
        {
            // Another concurrent request beat us to the insert.
            // Detach every entity we tried to add so the change tracker is clean,
            // then fall through to the reload below.
            foreach (EntityEntry entry in _db.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
            }
        }

        // Reload with StageDefinition navigation — works whether we inserted or lost the race
        return await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns true for SQL errors that indicate a concurrent process already inserted
    /// the same rows (unique-key violation 2627/2601) or that this process was chosen
    /// as a deadlock victim (1205).
    /// Uses reflection to read SqlException.Number without taking a direct dependency
    /// on Microsoft.Data.SqlClient from the Core project.
    /// </summary>
    private static bool IsDuplicateKeyOrDeadlockException(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            // SqlException type is in Microsoft.Data.SqlClient or System.Data.SqlClient
            if (inner.GetType().Name == "SqlException")
            {
                var numberProp = inner.GetType().GetProperty("Number");
                if (numberProp?.GetValue(inner) is int number &&
                    (number is 2627 or 2601 or 1205))
                    return true;
            }
            inner = inner.InnerException;
        }
        return false;
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
            .Where(t => t.CaseId == caseId && t.Status == Insolvio.Domain.Enums.TaskStatus.Done)
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
            validation,
            stage.DeadlineDate,
            stage.Notes,
            stage.DeadlineOverrideNote,
            stage.DeadlineOverriddenBy,
            stage.DeadlineOverriddenAt
        );
    }

    private static T Deserialize<T>(string json) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(json, _jsonOpts) ?? new T(); }
        catch { return new T(); }
    }

    // ── Case close ──────────────────────────────────────────────────────

    public async Task<CaseCloseabilityDto> GetCloseabilityAsync(Guid caseId, CancellationToken ct)
    {
        var stages = await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        var pending = stages
            .Where(s => s.Status != CaseWorkflowStatus.Completed && s.Status != CaseWorkflowStatus.Skipped)
            .Select(s => new StageReadinessItem(
                s.StageKey,
                s.StageDefinition?.Name ?? s.StageKey,
                s.Status.ToString()))
            .ToList();

        return new CaseCloseabilityDto(pending.Count == 0, pending);
    }

    public async Task CloseCaseAsync(Guid caseId, string? explanation, bool overridePendingStages, CancellationToken ct)
    {
        var stages = await _db.CaseWorkflowStages
            .Where(s => s.CaseId == caseId)
            .ToListAsync(ct);

        var pending = stages
            .Where(s => s.Status != CaseWorkflowStatus.Completed && s.Status != CaseWorkflowStatus.Skipped)
            .ToList();

        if (pending.Count > 0 && !overridePendingStages)
            throw new BusinessException(
                $"Cannot close case: {pending.Count} stage(s) are not yet completed or skipped.");

        if (overridePendingStages && string.IsNullOrWhiteSpace(explanation))
            throw new BusinessException(
                "An explanation is required when overriding pending stages to close the case.");

        if (overridePendingStages && pending.Count > 0)
        {
            var overrideNote = $"Force-closed by {_currentUser.Email}: {explanation}";
            foreach (var stage in pending)
            {
                stage.Status = CaseWorkflowStatus.Skipped;
                stage.Notes = overrideNote;
                stage.CompletedAt = DateTime.UtcNow;
                stage.CompletedBy = _currentUser.Email;
            }
        }

        var insolvencyCase = await _db.InsolvencyCases.FindAsync([caseId], ct)
            ?? throw new NotFoundException($"Case {caseId} not found");

        insolvencyCase.Status = "Closed";
        insolvencyCase.ClosureDate = DateTime.UtcNow;
        insolvencyCase.ClosureNotes = explanation;
        insolvencyCase.StatusChangedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogWorkflowAsync(
            "CaseClosed",
            caseId,
            new { explanation, overridePendingStages, overriddenStageCount = pending.Count },
            "Warning");
    }

    // ── Stage deadline override ─────────────────────────────────────────

    public async Task ReopenCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var insolvencyCase = await _db.InsolvencyCases.FindAsync([caseId], ct)
            ?? throw new NotFoundException($"Case {caseId} not found");

        if (insolvencyCase.Status != "Closed")
            throw new BusinessException("Case is not closed and cannot be reopened.");

        insolvencyCase.Status = "Active";
        insolvencyCase.StatusChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogWorkflowAsync(
            "CaseReopened",
            caseId,
            new { reopenedBy = _currentUser.Email },
            "Info");
    }

    public async Task<CaseWorkflowStageDto> SetStageDeadlineAsync(
        Guid caseId, string stageKey, DateTime newDate, string note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new BusinessException("A note is required when overriding a stage deadline.");

        var stage = await _db.CaseWorkflowStages
            .Include(s => s.StageDefinition)
            .Where(s => s.CaseId == caseId && s.StageKey == stageKey)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Stage '{stageKey}' not found on case {caseId}");

        var previousDate = stage.DeadlineDate;
        stage.DeadlineDate = newDate.ToUniversalTime();
        stage.DeadlineOverrideNote = note;
        stage.DeadlineOverriddenBy = _currentUser.Email;
        stage.DeadlineOverriddenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogWorkflowAsync(
            "StageDeadlineOverridden",
            caseId,
            new { stageKey, previousDate, newDate, note, overriddenBy = _currentUser.Email },
            "Warning");

        return ToDto(stage, null);
    }
}

