using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/phases")]
[Authorize]
[RequirePermission(Permission.PhaseView)]
public class CasePhasesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CasePhasesController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid caseId)
    {
        var phases = await _db.CasePhases
            .Where(p => p.CaseId == caseId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(phases.Select(p => p.ToDto()));
    }

    /// <summary>
    /// Initialize all workflow phases for a case based on its procedure type.
    /// Idempotent — skips if phases already exist.
    /// </summary>
    [HttpPost("initialize")]
    [RequirePermission(Permission.PhaseInitialize)]
    public async Task<IActionResult> Initialize(Guid caseId)
    {
        var insolvencyCase = await _db.InsolvencyCases.FirstOrDefaultAsync(c => c.Id == caseId);
        if (insolvencyCase == null) return NotFound(new { message = "Case not found" });

        var existing = await _db.CasePhases.AnyAsync(p => p.CaseId == caseId);
        if (existing) return Ok(new { message = "Phases already initialized" });

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

        await _db.SaveChangesAsync();
        await _audit.LogWorkflowAsync("Phase.Initialized", caseId,
  new { procedureType = insolvencyCase.ProcedureType.ToString(), phaseCount = phaseTemplates.Count });

        var phases = await _db.CasePhases
            .Where(p => p.CaseId == caseId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(phases.Select(p => p.ToDto()));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.PhaseEdit)]
    public async Task<IActionResult> Update(Guid caseId, Guid id, [FromBody] UpdateCasePhaseRequest req)
    {
        var phase = await _db.CasePhases.FirstOrDefaultAsync(p => p.Id == id && p.CaseId == caseId);
        if (phase == null) return NotFound();

        var oldStatus = phase.Status.ToString();

        if (req.Status != null && Enum.TryParse<PhaseStatus>(req.Status, true, out var status))
        {
            phase.Status = status;
            if (status == PhaseStatus.InProgress && phase.StartedOn == null)
                phase.StartedOn = DateTime.UtcNow;
            if (status == PhaseStatus.Completed && phase.CompletedOn == null)
                phase.CompletedOn = DateTime.UtcNow;
        }

        if (req.StartedOn.HasValue) phase.StartedOn = req.StartedOn;
        if (req.CompletedOn.HasValue) phase.CompletedOn = req.CompletedOn;
        if (req.DueDate.HasValue) phase.DueDate = req.DueDate;
        if (req.Notes != null) phase.Notes = req.Notes;
        if (req.CourtDecisionRef != null) phase.CourtDecisionRef = req.CourtDecisionRef;

        await _db.SaveChangesAsync();
        await _audit.LogWorkflowAsync("Phase.Updated", caseId,
  new { phaseId = id, phaseType = phase.PhaseType.ToString(), oldStatus, newStatus = phase.Status.ToString() });
        return Ok(phase.ToDto());
    }

    /// <summary>
    /// Advance to the next phase: complete current, start next.
    /// </summary>
    [HttpPost("advance")]
    [RequirePermission(Permission.PhaseAdvance)]
    public async Task<IActionResult> Advance(Guid caseId)
    {
        var phases = await _db.CasePhases
            .Where(p => p.CaseId == caseId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        if (phases.Count == 0)
            return BadRequest(new { message = "No phases initialized" });

        var current = phases.FirstOrDefault(p => p.Status == PhaseStatus.InProgress);
        if (current == null)
            return BadRequest(new { message = "No phase currently in progress" });

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
        }

        await _db.SaveChangesAsync();
        await _audit.LogWorkflowAsync("Phase.Advanced", caseId,
    new { completedPhase = current.PhaseType.ToString(), nextPhase = nextPhase?.PhaseType.ToString() });
        return Ok(phases.Select(p => p.ToDto()));
    }

    private static List<(PhaseType Type, int Order)> GetPhasesForProcedure(ProcedureType procedureType)
    {
        return procedureType switch
        {
            ProcedureType.Insolventa => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1),
                (PhaseType.ObservationPeriod, 2),
                (PhaseType.CreditorNotification, 3),
                (PhaseType.ClaimsFiling, 4),
                (PhaseType.PreliminaryClaimsTable, 5),
                (PhaseType.ClaimsContestations, 6),
                (PhaseType.DefinitiveClaimsTable, 7),
                (PhaseType.CausesReport, 8),
                (PhaseType.ReorganizationPlanProposal, 9),
                (PhaseType.ReorganizationPlanVoting, 10),
                (PhaseType.ReorganizationPlanConfirmation, 11),
                (PhaseType.ReorganizationExecution, 12),
                (PhaseType.FinalReport, 13),
                (PhaseType.ProcedureClosure, 14),
            },
            ProcedureType.Reorganizare => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1),
                (PhaseType.ObservationPeriod, 2),
                (PhaseType.CreditorNotification, 3),
                (PhaseType.ClaimsFiling, 4),
                (PhaseType.PreliminaryClaimsTable, 5),
                (PhaseType.DefinitiveClaimsTable, 6),
                (PhaseType.ReorganizationPlanProposal, 7),
                (PhaseType.ReorganizationPlanVoting, 8),
                (PhaseType.ReorganizationPlanConfirmation, 9),
                (PhaseType.ReorganizationExecution, 10),
                (PhaseType.FinalReport, 11),
                (PhaseType.ProcedureClosure, 12),
            },
            ProcedureType.Faliment or ProcedureType.FalimentSimplificat => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1),
                (PhaseType.CreditorNotification, 2),
                (PhaseType.ClaimsFiling, 3),
                (PhaseType.PreliminaryClaimsTable, 4),
                (PhaseType.ClaimsContestations, 5),
                (PhaseType.DefinitiveClaimsTable, 6),
                (PhaseType.AssetLiquidation, 7),
                (PhaseType.CreditorDistribution, 8),
                (PhaseType.FinalReport, 9),
                (PhaseType.ProcedureClosure, 10),
            },
            _ => new List<(PhaseType, int)>
            {
                (PhaseType.OpeningRequest, 1),
                (PhaseType.CreditorNotification, 2),
                (PhaseType.ClaimsFiling, 3),
                (PhaseType.PreliminaryClaimsTable, 4),
                (PhaseType.DefinitiveClaimsTable, 5),
                (PhaseType.FinalReport, 6),
                (PhaseType.ProcedureClosure, 7),
            },
        };
    }
}
