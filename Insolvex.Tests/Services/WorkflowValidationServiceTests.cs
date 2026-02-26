using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Insolvex.API.Services;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Insolvex.Tests.Helpers;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Tests.Services;

public class WorkflowValidationServiceTests
{
    private static WorkflowValidationService CreateService(Insolvex.API.Data.ApplicationDbContext db)
        => new(db, Mock.Of<ILogger<WorkflowValidationService>>());

    // ?? Stage gate validation ???????????????????????????????

  [Fact]
 public async Task ValidateStage_Intake_FailsWhen_NoNoticeDateOrDebtor()
    {
        using var db = TestDbFactory.Create();
      var tenantId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        db.InsolvencyCases.Add(new InsolvencyCase
        {
            Id = caseId,
         TenantId = tenantId,
    CaseNumber = "TEST/001",
            DebtorName = "",        // missing
     DebtorCui = null,       // missing
  Stage = CaseStage.Intake,
        ProcedureType = ProcedureType.Other, // must be non-Other
  });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ValidateStageAsync(caseId);

        result.CanAdvance.Should().BeFalse();
        result.Rules.Should().Contain(r => !r.Passed && r.Description.Contains("Debtor"));
    }

    [Fact]
    public async Task ValidateStage_Intake_PassesWhen_AllGatesMet()
    {
  using var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
      var caseId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.InsolvencyCases.Add(new InsolvencyCase
        {
       Id = caseId,
         TenantId = tenantId,
    CaseNumber = "TEST/002",
            DebtorName = "Test SRL",
     DebtorCui = "RO12345",
  Stage = CaseStage.Intake,
         ProcedureType = ProcedureType.FalimentSimplificat,
    OpeningDate = DateTime.UtcNow,
   AssignedToUserId = userId,
    ClaimsDeadline = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ValidateStageAsync(caseId);

        result.CanAdvance.Should().BeTrue();
        result.NextStage.Should().Be(CaseStage.EligibilitySetup);
        result.Rules.Should().OnlyContain(r => r.Passed);
    }

    [Fact]
    public async Task ValidateStage_ReturnsError_ForNonExistentCase()
    {
  using var db = TestDbFactory.Create();
        var svc = CreateService(db);
    var result = await svc.ValidateStageAsync(Guid.NewGuid());

        result.Error.Should().Be("Case not found");
        result.CanAdvance.Should().BeFalse();
  }

    // ?? Stage advance + auto-task generation ????????????????

  [Fact]
    public async Task AdvanceStage_Fails_WhenGatesNotMet()
    {
     using var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
     var caseId = Guid.NewGuid();

     db.InsolvencyCases.Add(new InsolvencyCase
      {
          Id = caseId,
 TenantId = tenantId,
   CaseNumber = "TEST/003",
            DebtorName = "",
         Stage = CaseStage.Intake,
      ProcedureType = ProcedureType.Other,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
     var result = await svc.AdvanceStageAsync(caseId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("gates");
        result.FailedRules.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AdvanceStage_AutoGeneratesTasks_ForNewStage()
    {
        using var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
  var companyId = Guid.NewGuid();

        db.Companies.Add(new Company
   {
     Id = companyId,
 TenantId = tenantId,
  Name = "Debtor SRL",
        });

        db.InsolvencyCases.Add(new InsolvencyCase
        {
          Id = caseId,
       TenantId = tenantId,
      CaseNumber = "TEST/004",
     DebtorName = "Debtor SRL",
        DebtorCui = "RO99999",
         Stage = CaseStage.Intake,
       ProcedureType = ProcedureType.FalimentSimplificat,
            OpeningDate = DateTime.UtcNow,
            AssignedToUserId = userId,
   ClaimsDeadline = DateTime.UtcNow.AddDays(30),
      CompanyId = companyId,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.AdvanceStageAsync(caseId, userId);

        result.Success.Should().BeTrue();
      result.NewStage.Should().Be(CaseStage.EligibilitySetup);
        result.PreviousStage.Should().Be(CaseStage.Intake);

     // Auto-generated tasks for EligibilitySetup stage
        var tasks = db.CompanyTasks
       .Where(t => t.CaseId == caseId && t.Stage == CaseStage.EligibilitySetup)
        .ToList();

        tasks.Should().NotBeEmpty();
tasks.Should().AllSatisfy(t =>
   {
            t.Deadline.Should().NotBeNull();
            t.AssignedToUserId.Should().Be(userId);
 t.Status.Should().Be(TaskStatus.Open);
        });
    }

    [Fact]
    public async Task AdvanceStage_CreatesAuditLog()
    {
using var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
     var caseId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.InsolvencyCases.Add(new InsolvencyCase
        {
            Id = caseId,
       TenantId = tenantId,
   CaseNumber = "TEST/005",
       DebtorName = "Test SRL",
            DebtorCui = "RO88888",
     Stage = CaseStage.Intake,
    ProcedureType = ProcedureType.Faliment,
        OpeningDate = DateTime.UtcNow,
       AssignedToUserId = userId,
    KeyDeadlinesJson = "{}",
        });
        await db.SaveChangesAsync();

    var svc = CreateService(db);
      await svc.AdvanceStageAsync(caseId, userId);

  var auditLog = db.AuditLogs.FirstOrDefault(a => a.Action == "StageAdvance" && a.EntityId == caseId);
     auditLog.Should().NotBeNull();
        auditLog!.Changes.Should().Contain("Intake");
     auditLog.Changes.Should().Contain("EligibilitySetup");
    }

    // ?? Stage timeline ??????????????????????????????????????

    [Fact]
    public async Task GetStageTimeline_ReturnsAllStages_WithCorrectStatuses()
 {
        using var db = TestDbFactory.Create();
        var caseId = Guid.NewGuid();

        db.InsolvencyCases.Add(new InsolvencyCase
      {
        Id = caseId,
            TenantId = Guid.NewGuid(),
            CaseNumber = "TEST/006",
  DebtorName = "Test SRL",
            Stage = CaseStage.CreditorClaims, // Stage 3
    });
     await db.SaveChangesAsync();

        var svc = CreateService(db);
        var timeline = await svc.GetStageTimelineAsync(caseId);

        timeline.Should().HaveCount(9); // 0 through 8

     timeline.Where(s => s.Status == "completed").Should().HaveCount(3); // Intake, Eligibility, FormalNotifications
        timeline.Where(s => s.Status == "current").Should().HaveCount(1);
        timeline.First(s => s.Status == "current").Stage.Should().Be(CaseStage.CreditorClaims);
        timeline.Where(s => s.Status == "pending").Should().HaveCount(5);
    }

    // ?? Category resolution ?????????????????????????????????

    [Fact]
    public async Task AutoGeneratedTasks_HaveCorrectCategories()
    {
        using var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
      var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

     db.Companies.Add(new Company { Id = companyId, TenantId = tenantId, Name = "X" });
        db.InsolvencyCases.Add(new InsolvencyCase
        {
            Id = caseId,
     TenantId = tenantId,
            CaseNumber = "TEST/007",
      DebtorName = "X",
  DebtorCui = "RO1",
        Stage = CaseStage.Intake,
 ProcedureType = ProcedureType.Faliment,
  OpeningDate = DateTime.UtcNow,
          AssignedToUserId = userId,
        ClaimsDeadline = DateTime.UtcNow.AddDays(30),
      CompanyId = companyId,
        });
    await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AdvanceStageAsync(caseId, userId);

        var tasks = db.CompanyTasks.Where(t => t.CaseId == caseId).ToList();

        // Tasks containing "notice" should be Email category
     tasks.Where(t => t.Title.Contains("notice", StringComparison.OrdinalIgnoreCase))
            .Should().OnlyContain(t => t.Category == "Email");

        // Tasks containing "notice" should be marked critical
        tasks.Where(t => t.Title.Contains("notice", StringComparison.OrdinalIgnoreCase))
            .Should().OnlyContain(t => t.IsCriticalDeadline);
    }
}
