using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.API.Services;

public sealed class CasePhasesService : ICasePhasesService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ICaseEventService _caseEvents;

    public CasePhasesService(ApplicationDbContext db, ICurrentUserService currentUser,
        IAuditService audit, ICaseEventService caseEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _caseEvents = caseEvents;
    }

    public async Task<List<CasePhaseDto>> GetAllAsync(Guid caseId, CancellationToken ct = default)
        => await _db.CasePhases
            .Where(p => p.CaseId == caseId)
            .OrderBy(p => p.SortOrder)
            .Select(p => p.ToDto())
            .ToListAsync(ct);

    public async Task<List<CasePhaseDto>> InitializeAsync(Guid caseId, CancellationToken ct = default)
    {
        var insolvencyCase = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId, ct)
            ?? throw new NotFoundException("InsolvencyCase", caseId);

        var existing = await _db.CasePhases.AnyAsync(p => p.CaseId == caseId, ct);
        if (existing)
            return await GetAllAsync(caseId, ct);

        var phaseTemplates = GetPhasesForProcedure(insolvencyCase.ProcedureType);

        foreach (var (phaseType, order) in phaseTemplates)
        {
            _db.CasePhases.Add(new CasePhase
            {
                CaseId = caseId,
                PhaseType = phaseType,
                Status = order == 1 ? PhaseStatus.InProgress : PhaseStatus.NotStarted,
                SortOrder = order,
            });
        }

        await _db.SaveChangesAsync(ct);

        var userId = _currentUser.UserId ?? Guid.Empty;
        var firstPhaseType = phaseTemplates.OrderBy(p => p.Order).First().Type;
        await GeneratePhaseTasksAsync(insolvencyCase, firstPhaseType, userId);
        await _db.SaveChangesAsync(ct);

        await _audit.LogWorkflowAsync("Case Phase Initialized", caseId,
            new { procedureType = insolvencyCase.ProcedureType.ToString(), phaseCount = phaseTemplates.Count });

        return await GetAllAsync(caseId, ct);
    }

    public async Task<CasePhaseDto> UpdateAsync(Guid caseId, Guid phaseId, UpdateCasePhaseRequest request, CancellationToken ct = default)
    {
        var phase = await _db.CasePhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CasePhase", phaseId);

        var oldStatus = phase.Status.ToString();

        if (request.Status != null && Enum.TryParse<PhaseStatus>(request.Status, true, out var status))
        {
            phase.Status = status;
            if (status == PhaseStatus.InProgress && phase.StartedOn == null)
                phase.StartedOn = DateTime.UtcNow;
            if (status == PhaseStatus.Completed && phase.CompletedOn == null)
                phase.CompletedOn = DateTime.UtcNow;
        }
        if (request.StartedOn.HasValue) phase.StartedOn = request.StartedOn;
        if (request.CompletedOn.HasValue) phase.CompletedOn = request.CompletedOn;
        if (request.DueDate.HasValue) phase.DueDate = request.DueDate;
        if (request.Notes != null) phase.Notes = request.Notes;
        if (request.CourtDecisionRef != null) phase.CourtDecisionRef = request.CourtDecisionRef;

        await _db.SaveChangesAsync(ct);
        await _audit.LogWorkflowAsync("Case Phase Updated", caseId,
            new { phaseId, phaseType = phase.PhaseType.ToString(), oldStatus, newStatus = phase.Status.ToString() });

        return phase.ToDto();
    }

    public async Task<CasePhaseDto> AdvanceAsync(Guid caseId, CancellationToken ct = default)
    {
        var phases = await _db.CasePhases
            .Where(p => p.CaseId == caseId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        if (phases.Count == 0)
            throw new BusinessException("No phases initialized for this case.");

        var current = phases.FirstOrDefault(p => p.Status == PhaseStatus.InProgress)
            ?? throw new BusinessException("No phase currently in progress.");

        // ── Block advance until all open/blocked tasks for this case are done ──
        var openTaskCount = await _db.CompanyTasks
            .CountAsync(t => t.CaseId == caseId
                          && (t.Status == TaskStatus.Open || t.Status == TaskStatus.Blocked), ct);

        if (openTaskCount > 0)
            throw new BusinessException(
                $"Cannot advance to the next phase: {openTaskCount} task(s) must be completed first. " +
                "Complete or mark all open tasks as Done before advancing.");

        current.Status = PhaseStatus.Completed;
        current.CompletedOn = DateTime.UtcNow;

        var nextPhase = phases
            .Where(p => p.SortOrder > current.SortOrder && p.Status == PhaseStatus.NotStarted)
            .OrderBy(p => p.SortOrder)
            .FirstOrDefault();

        if (nextPhase != null)
        {
            nextPhase.Status = PhaseStatus.InProgress;
            nextPhase.StartedOn = DateTime.UtcNow;
            var insolvencyCase = await _db.InsolvencyCases.FindAsync(new object[] { caseId }, ct);
            if (insolvencyCase != null)
            {
                var userId = _currentUser.UserId ?? Guid.Empty;
                await GeneratePhaseTasksAsync(insolvencyCase, nextPhase.PhaseType, userId);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Record timeline event
        await _caseEvents.RecordPhaseEventAsync(caseId,
            "Phase.Advanced",
            current.PhaseType.ToString(),
            nextPhase?.PhaseType.ToString(),
            description: null,
            ct);

        await _audit.LogWorkflowAsync("Case Phase Advanced", caseId,
            new { completedPhase = current.PhaseType.ToString(), nextPhase = nextPhase?.PhaseType.ToString() });

        return nextPhase?.ToDto() ?? current.ToDto();
    }

    public async Task<object> GetRequirementsAsync(Guid caseId, Guid phaseId, CancellationToken ct = default)
    {
        var phase = await _db.CasePhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CasePhase", phaseId);

        var config = PhaseTaskDefinitions.GetPhase(phase.PhaseType);
        if (config == null)
            return new { phase = phase.PhaseType.ToString(), requiredTasks = Array.Empty<string>() };

        return new
        {
            phase = phase.PhaseType.ToString(),
            goal = config.Goal,
            requiredTasks = config.RequiredTasks,
            requiredDocTypes = config.RequiredDocTypes,
            requiredFields = config.RequiredFields,
            autoTaskCategories = config.AutoTaskCategories,
        };
    }

    public async Task<int> GenerateTasksAsync(Guid caseId, Guid phaseId, CancellationToken ct = default)
    {
        var phase = await _db.CasePhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.CaseId == caseId, ct)
            ?? throw new NotFoundException("CasePhase", phaseId);

        var insolvencyCase = await _db.InsolvencyCases.FindAsync(new object[] { caseId }, ct)
            ?? throw new NotFoundException("InsolvencyCase", caseId);

        var userId = _currentUser.UserId ?? Guid.Empty;
        var count = await GeneratePhaseTasksAsync(insolvencyCase, phase.PhaseType, userId);
        await _db.SaveChangesAsync(ct);

        await _audit.LogWorkflowAsync("Phase Tasks Auto-Generated", caseId,
            new { phaseId, phaseType = phase.PhaseType.ToString(), tasksGenerated = count });
        return count;
    }

    private async Task<int> GeneratePhaseTasksAsync(InsolvencyCase cas, PhaseType phaseType, Guid userId)
    {
        var config = PhaseTaskDefinitions.GetPhase(phaseType);
        if (config == null || !cas.CompanyId.HasValue) return 0;

        var existingTitles = await _db.CompanyTasks
            .Where(t => t.CaseId == cas.Id)
            .Select(t => t.Title)
            .ToListAsync();

        var count = 0;
        foreach (var taskTitle in config.RequiredTasks)
        {
            if (existingTitles.Any(t => t.Contains(taskTitle, StringComparison.OrdinalIgnoreCase)))
                continue;

            var category = PhaseTaskDefinitions.ResolveCategory(taskTitle, config.AutoTaskCategories);
            var isCritical = taskTitle.Contains("notice", StringComparison.OrdinalIgnoreCase)
                || taskTitle.Contains("BPI", StringComparison.OrdinalIgnoreCase)
                || taskTitle.Contains("notification", StringComparison.OrdinalIgnoreCase)
                || taskTitle.Contains("deadline", StringComparison.OrdinalIgnoreCase);

            _db.CompanyTasks.Add(new CompanyTask
            {
                TenantId = cas.TenantId,
                CompanyId = cas.CompanyId.Value,
                CaseId = cas.Id,
                Title = taskTitle,
                Description = $"Auto-generated for phase '{phaseType.ToString().Replace("(", " ").Replace(")", "")}'. Goal: {config.Goal}",
                Category = category,
                Deadline = DateTime.UtcNow.AddDays(7 + count * 2),
                DeadlineSource = "CompanyDefault",
                IsCriticalDeadline = isCritical,
                Status = TaskStatus.Open,
                AssignedToUserId = cas.AssignedToUserId ?? userId,
                CreatedByUserId = userId,
            });
            count++;
        }

        return count;
    }

    private static List<(PhaseType Type, int Order)> GetPhasesForProcedure(ProcedureType procedureType)
    {
        return procedureType switch
        {
            ProcedureType.Insolventa => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1), (PhaseType.ObservationPeriod, 2),
                (PhaseType.CreditorNotification, 3), (PhaseType.ClaimsFiling, 4),
                (PhaseType.PreliminaryClaimsTable, 5), (PhaseType.ClaimsContestations, 6),
                (PhaseType.DefinitiveClaimsTable, 7), (PhaseType.CausesReport, 8),
                (PhaseType.ReorganizationPlanProposal, 9), (PhaseType.ReorganizationPlanVoting, 10),
                (PhaseType.ReorganizationPlanConfirmation, 11), (PhaseType.ReorganizationExecution, 12),
                (PhaseType.FinalReport, 13), (PhaseType.ProcedureClosure, 14),
            },
            ProcedureType.Reorganizare => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1), (PhaseType.ObservationPeriod, 2),
                (PhaseType.CreditorNotification, 3), (PhaseType.ClaimsFiling, 4),
                (PhaseType.PreliminaryClaimsTable, 5), (PhaseType.DefinitiveClaimsTable, 6),
                (PhaseType.ReorganizationPlanProposal, 7), (PhaseType.ReorganizationPlanVoting, 8),
                (PhaseType.ReorganizationPlanConfirmation, 9), (PhaseType.ReorganizationExecution, 10),
                (PhaseType.FinalReport, 11), (PhaseType.ProcedureClosure, 12),
            },
            ProcedureType.Faliment or ProcedureType.FalimentSimplificat => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1), (PhaseType.CreditorNotification, 2),
                (PhaseType.ClaimsFiling, 3), (PhaseType.PreliminaryClaimsTable, 4),
                (PhaseType.ClaimsContestations, 5), (PhaseType.DefinitiveClaimsTable, 6),
                (PhaseType.AssetLiquidation, 7), (PhaseType.CreditorDistribution, 8),
                (PhaseType.FinalReport, 9), (PhaseType.ProcedureClosure, 10),
            },
            _ => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1), (PhaseType.CreditorNotification, 2),
                (PhaseType.ClaimsFiling, 3), (PhaseType.PreliminaryClaimsTable, 4),
                (PhaseType.DefinitiveClaimsTable, 5), (PhaseType.FinalReport, 6),
                (PhaseType.ProcedureClosure, 7),
            },
        };
    }
}
