using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Insolvex.API.Data;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.API.Services;

/// <summary>
/// Validates stage gate rules and advances cases through the workflow.
/// On stage advance, auto-generates required tasks for the new stage.
/// </summary>
public class WorkflowValidationService
{
  private readonly ApplicationDbContext _db;
  private readonly ILogger<WorkflowValidationService> _logger;

  public WorkflowValidationService(ApplicationDbContext db, ILogger<WorkflowValidationService> logger)
  {
    _db = db;
    _logger = logger;
  }

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
  /// Auto-generates required tasks for the new stage.
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

    var caseEntity = await _db.InsolvencyCases
.Include(c => c.Company)
 .FirstOrDefaultAsync(c => c.Id == caseId);
    if (caseEntity == null)
      return new StageAdvanceResult { Success = false, Error = "Case not found" };

    var previousStage = caseEntity.Stage;
    caseEntity.StageCompletedAt = DateTime.UtcNow;
    caseEntity.Stage = validation.NextStage.Value;
    caseEntity.StageEnteredAt = DateTime.UtcNow;

    // Auto-generate tasks for the new stage
    var newStageDef = WorkflowDefinition.GetStage(validation.NextStage.Value);
    if (newStageDef != null && caseEntity.CompanyId.HasValue)
    {
      var autoTaskCount = await GenerateStageTasksAsync(
caseEntity, newStageDef, userId, caseEntity.CompanyId.Value);
      _logger.LogInformation("Auto-generated {Count} tasks for stage {Stage} on case {CaseId}",
autoTaskCount, validation.NextStage.Value, caseId);
    }

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

  /// <summary>
  /// Auto-generate tasks for a stage based on the WorkflowDefinition.RequiredTasks.
  /// </summary>
  private async Task<int> GenerateStageTasksAsync(
      InsolvencyCase cas, StageDefinition stageDef, Guid userId, Guid companyId)
  {
    var existingTitles = await _db.CompanyTasks
.Where(t => t.CaseId == cas.Id && t.Stage == stageDef.Stage)
.Select(t => t.Title)
        .ToListAsync();

    var count = 0;
    var baseDeadline = DateTime.UtcNow;

    foreach (var taskTitle in stageDef.RequiredTasks)
    {
      // Skip if a task with similar title already exists for this stage
      if (existingTitles.Any(t => t.Contains(taskTitle, StringComparison.OrdinalIgnoreCase)))
        continue;

      var category = ResolveCategory(taskTitle, stageDef.AutoTaskCategories);
      var isCritical = taskTitle.Contains("notice", StringComparison.OrdinalIgnoreCase)
|| taskTitle.Contains("Templates-Ro", StringComparison.OrdinalIgnoreCase)
  || taskTitle.Contains("critical", StringComparison.OrdinalIgnoreCase);

      _db.CompanyTasks.Add(new CompanyTask
      {
        TenantId = cas.TenantId,
        CompanyId = companyId,
        CaseId = cas.Id,
        Stage = stageDef.Stage,
        Title = $"{taskTitle} � {cas.DebtorName}",
        Description = $"Auto-generated for stage: {stageDef.Name}. Goal: {stageDef.Goal}",
        Category = category,
        Deadline = baseDeadline.AddDays(7 + count * 2), // stagger deadlines
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

  private static string ResolveCategory(string taskTitle, string[] autoCategories)
  {
    var lower = taskTitle.ToLowerInvariant();
    if (lower.Contains("email") || lower.Contains("send") || lower.Contains("notice")) return "Email";
    if (lower.Contains("generate") || lower.Contains("document") || lower.Contains("template") || lower.Contains("upload") || lower.Contains("report")) return "Document";
    if (lower.Contains("meeting") || lower.Contains("attendance") || lower.Contains("vote")) return "Meeting";
    if (lower.Contains("review") || lower.Contains("verify") || lower.Contains("confirm") || lower.Contains("check")) return "Review";
    if (lower.Contains("file") || lower.Contains("filing")) return "Filing";
    if (lower.Contains("payment") || lower.Contains("distribut")) return "Payment";
    if (lower.Contains("compliance")) return "Compliance";
    return autoCategories.FirstOrDefault() ?? "Review";
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
