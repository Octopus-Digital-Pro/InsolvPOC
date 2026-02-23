using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Insolvex.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Insolvex.API.Services;

/// <summary>
/// Validates stage gate rules and advances cases through the workflow.
/// </summary>
public class WorkflowValidationService
{
    private readonly ApplicationDbContext _db;

    public WorkflowValidationService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Check if the case can advance from its current stage.
    /// Returns a result with pass/fail and details on each validation rule.
    /// </summary>
    public async Task<StageValidationResult> ValidateStageAsync(Guid caseId)
    {
        var caseEntity = await _db.InsolvencyCases
            .Include(c => c.Parties).ThenInclude(p => p.Company)
            .Include(c => c.Documents)
          .FirstOrDefaultAsync(c => c.Id == caseId);

        if (caseEntity == null)
      return new StageValidationResult { CaseId = caseId, Error = "Case not found" };

        var stageDef = WorkflowDefinition.GetStage(caseEntity.Stage);
        if (stageDef == null)
        return new StageValidationResult { CaseId = caseId, Error = $"No workflow definition for stage {caseEntity.Stage}" };

        var tasks = await _db.CompanyTasks
.Where(t => t.CaseId == caseId)
            .ToListAsync();

        var emails = await _db.ScheduledEmails
      .Where(e => e.TenantId == caseEntity.TenantId)
        .ToListAsync();

        var ctx = new ValidationContext
        {
        Case = caseEntity,
            Parties = caseEntity.Parties.ToList(),
            Documents = caseEntity.Documents.ToList(),
  Tasks = tasks,
  Emails = emails,
        };

        var ruleResults = new List<RuleResult>();
        var allPassed = true;

        foreach (var rule in stageDef.ValidationRules)
        {
            bool passed;
  try
            {
 passed = rule.Predicate(ctx);
            }
         catch
            {
       passed = false;
            }

        ruleResults.Add(new RuleResult { Description = rule.Description, Passed = passed });
            if (!passed) allPassed = false;
        }

   return new StageValidationResult
        {
            CaseId = caseId,
       CurrentStage = caseEntity.Stage,
     StageName = stageDef.Name,
         NextStage = stageDef.NextStage,
      CanAdvance = allPassed,
   Rules = ruleResults,
     };
    }

    /// <summary>
    /// Advance the case to the next stage if all validation rules pass.
    /// </summary>
    public async Task<StageAdvanceResult> AdvanceStageAsync(Guid caseId, Guid userId)
    {
    var validation = await ValidateStageAsync(caseId);
      if (validation.Error != null)
            return new StageAdvanceResult { Success = false, Error = validation.Error };

     if (!validation.CanAdvance)
            return new StageAdvanceResult
            {
        Success = false,
Error = "Validation gates not met",
    FailedRules = validation.Rules.Where(r => !r.Passed).Select(r => r.Description).ToList(),
        };

        if (validation.NextStage == null)
            return new StageAdvanceResult { Success = false, Error = "Case is at the final stage" };

  var caseEntity = await _db.InsolvencyCases.FindAsync(caseId);
        if (caseEntity == null)
         return new StageAdvanceResult { Success = false, Error = "Case not found" };

        var previousStage = caseEntity.Stage;
        caseEntity.StageCompletedAt = DateTime.UtcNow;
     caseEntity.Stage = validation.NextStage.Value;
      caseEntity.StageEnteredAt = DateTime.UtcNow;

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
    TenantId = caseEntity.TenantId,
      Action = "StageAdvance",
    EntityType = "InsolvencyCase",
      EntityId = caseId,
   Changes = $"Advanced from {previousStage} to {validation.NextStage.Value}",
      UserEmail = userId.ToString(),
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        return new StageAdvanceResult
        {
     Success = true,
       PreviousStage = previousStage,
  NewStage = validation.NextStage.Value,
        };
    }

    /// <summary>
    /// Get the status of all stages for a case (for the stage timeline UI).
    /// </summary>
  public async Task<List<StageStatusInfo>> GetStageTimelineAsync(Guid caseId)
  {
        var caseEntity = await _db.InsolvencyCases.FindAsync(caseId);
        if (caseEntity == null) return new List<StageStatusInfo>();

    var currentOrder = WorkflowDefinition.GetStage(caseEntity.Stage)?.Order ?? 0;
        var result = new List<StageStatusInfo>();

        foreach (var stage in WorkflowDefinition.GetOrderedStages())
     {
        string status;
            if (stage.Order < currentOrder) status = "completed";
   else if (stage.Order == currentOrder) status = "current";
            else status = "pending";

       result.Add(new StageStatusInfo
         {
 Stage = stage.Stage,
            Order = stage.Order,
 Name = stage.Name,
       Goal = stage.Goal,
        Status = status,
     });
        }

  return result;
    }
}

public class StageValidationResult
{
    public Guid CaseId { get; set; }
    public CaseStage CurrentStage { get; set; }
    public string? StageName { get; set; }
 public CaseStage? NextStage { get; set; }
    public bool CanAdvance { get; set; }
    public List<RuleResult> Rules { get; set; } = new();
    public string? Error { get; set; }
}

public class RuleResult
{
    public string Description { get; set; } = string.Empty;
    public bool Passed { get; set; }
}

public class StageAdvanceResult
{
    public bool Success { get; set; }
    public CaseStage PreviousStage { get; set; }
    public CaseStage NewStage { get; set; }
    public string? Error { get; set; }
    public List<string>? FailedRules { get; set; }
}

public class StageStatusInfo
{
    public CaseStage Stage { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}
