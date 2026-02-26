using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Configuration;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Insolvex.Tests.Helpers;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Tests.Services;

public class CaseCreationServiceTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private static (CaseCreationService svc, Insolvex.API.Data.ApplicationDbContext db) CreateService()
    {
  var db = TestDbFactory.Create();
        var deadlineEngine = new DeadlineEngine(db);

  var currentUser = new Mock<ICurrentUserService>();
      currentUser.Setup(u => u.TenantId).Returns(TestTenantId);
        currentUser.Setup(u => u.UserId).Returns(TestUserId);
   currentUser.Setup(u => u.Email).Returns("test@insolvex.local");

 var audit = new Mock<IAuditService>();

        // Create a real MailMergeService with mocked dependencies that won't crash
    var storage = new Mock<IFileStorageService>();
      var templateGen = new Mock<TemplateGenerationService>(
 MockBehavior.Loose, db, storage.Object, Mock.Of<ILogger<TemplateGenerationService>>());
        var mailMergeOptions = Options.Create(new MailMergeOptions { TemplatesPath = "Templates-Ro" });
     var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

  var mailMerge = new MailMergeService(
            db, storage.Object, templateGen.Object, mailMergeOptions,
  env.Object, Mock.Of<ILogger<MailMergeService>>());

   var svc = new CaseCreationService(
       db, currentUser.Object, audit.Object, deadlineEngine,
     mailMerge, Mock.Of<ILogger<CaseCreationService>>());

        return (svc, db);
    }

    private static PendingUpload CreateTestUpload() => new()
    {
        Id = Guid.NewGuid(),
        OriginalFileName = "test_notice.pdf",
        StoredFileName = "test.pdf",
        FilePath = "/tmp/test.pdf",
FileSize = 1024,
        UploadedAt = DateTime.UtcNow,
     TenantId = TestTenantId,
    DetectedDocType = "original_notice",
        DetectedCaseNumber = "1234/85/2025",
        DetectedDebtorName = "SC Test Debtor SRL",
        DetectedCourtName = "Tribunalul Cluj",
        DetectedOpeningDate = new DateTime(2025, 3, 1),
    DetectedClaimsDeadline = new DateTime(2025, 4, 1),
        DetectedProcedureType = ProcedureType.FalimentSimplificat,
        Confidence = 0.95,
    };

  // ?? Core flow ???????????????????????????????????????????

    [Fact]
    public async Task CreateFromUpload_CreatesCase_WithCorrectFields()
    {
        var (svc, db) = CreateService();
        var upload = CreateTestUpload();
        db.Set<PendingUpload>().Add(upload);
     await db.SaveChangesAsync();

   var request = new CaseCreationRequest
        {
    CaseNumber = "1234/85/2025",
   DebtorName = "SC Test Debtor SRL",
            ProcedureType = "FalimentSimplificat",
            NoticeDate = new DateTime(2025, 3, 1),
     Parties = new()
    {
       new() { Role = "Debtor", Name = "SC Test Debtor SRL", FiscalId = "RO12345" },
      new() { Role = "InsolvencyPractitioner", Name = "Cabinet IP SRL", FiscalId = "RO99999" },
  },
        };

     var result = await svc.CreateFromUploadAsync(upload, request);

        result.CaseId.Should().NotBeEmpty();
        result.CaseNumber.Should().Be("1234/85/2025");
        result.NoticeDate.Should().Be(new DateTime(2025, 3, 1));
     result.Stage.Should().Be(CaseStage.Intake);

        // Case in DB
        var caseEntity = db.InsolvencyCases.First(c => c.Id == result.CaseId);
        caseEntity.DebtorName.Should().Be("SC Test Debtor SRL");
        caseEntity.ProcedureType.Should().Be(ProcedureType.FalimentSimplificat);
        caseEntity.NoticeDate.Should().Be(new DateTime(2025, 3, 1));
        caseEntity.AssignedToUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task CreateFromUpload_CreatesCompanies_ForEachParty()
    {
        var (svc, db) = CreateService();
        var upload = CreateTestUpload();
     db.Set<PendingUpload>().Add(upload);
    await db.SaveChangesAsync();

 var request = new CaseCreationRequest
        {
    Parties = new()
          {
          new() { Role = "Debtor", Name = "Debtor SRL", FiscalId = "RO111" },
    new() { Role = "SecuredCreditor", Name = "Creditor SA", FiscalId = "RO222" },
     new() { Role = "InsolvencyPractitioner", Name = "IP Cabinet", FiscalId = "RO333" },
            },
        };

 var result = await svc.CreateFromUploadAsync(upload, request);

      result.CompaniesCreated.Should().Be(3);
        result.PartiesCreated.Should().Be(3);

        var companies = db.Companies.ToList();
    companies.Should().HaveCount(3);
        companies.Should().Contain(c => c.CompanyType == CompanyType.Debtor);
        companies.Should().Contain(c => c.CompanyType == CompanyType.Creditor);
     companies.Should().Contain(c => c.CompanyType == CompanyType.InsolvencyPractitioner);
    }

    [Fact]
    public async Task CreateFromUpload_ReusesExistingCompany_WhenFound()
    {
        var (svc, db) = CreateService();

        // Pre-existing company
        var existingId = Guid.NewGuid();
        db.Companies.Add(new Company
  {
          Id = existingId,
            TenantId = TestTenantId,
        Name = "Existing Debtor SRL",
            CuiRo = "RO111",
        });
        await db.SaveChangesAsync();

        var upload = CreateTestUpload();
        db.Set<PendingUpload>().Add(upload);
        await db.SaveChangesAsync();

        var request = new CaseCreationRequest
        {
   Parties = new()
     {
     new() { Role = "Debtor", Name = "Existing Debtor SRL", FiscalId = "RO111" },
            },
        };

        var result = await svc.CreateFromUploadAsync(upload, request);

        // Should reuse, not create new
        db.Companies.Count().Should().Be(1);
     var party = db.CaseParties.First(p => p.CaseId == result.CaseId);
 party.CompanyId.Should().Be(existingId);
    }

    // ?? Phases ??????????????????????????????????????????????

    [Theory]
    [InlineData("FalimentSimplificat", 10)]
    [InlineData("Reorganizare", 13)]
    [InlineData("ConcordatPreventiv", 7)]
    public async Task CreateFromUpload_InitializesCorrectPhaseCount(string procType, int expectedPhases)
    {
      var (svc, db) = CreateService();
        var upload = CreateTestUpload();
        upload.DetectedProcedureType = Enum.Parse<ProcedureType>(procType);
        db.Set<PendingUpload>().Add(upload);
    await db.SaveChangesAsync();

    var request = new CaseCreationRequest
   {
            ProcedureType = procType,
       Parties = new() { new() { Role = "Debtor", Name = "X", FiscalId = "RO1" } },
        };

        var result = await svc.CreateFromUploadAsync(upload, request);

        result.PhasesCreated.Should().Be(expectedPhases);

        var phases = db.CasePhases.Where(p => p.CaseId == result.CaseId).OrderBy(p => p.SortOrder).ToList();
        phases.Should().HaveCount(expectedPhases);
        phases.First().Status.Should().Be(PhaseStatus.Completed);
  phases.Skip(1).First().Status.Should().Be(PhaseStatus.InProgress);
 phases.Skip(2).Should().OnlyContain(p => p.Status == PhaseStatus.NotStarted);
    }

    // ?? Tasks ???????????????????????????????????????????????

    [Fact]
  public async Task CreateFromUpload_GeneratesIntakeTasks_WithMandatoryDeadlines()
    {
    var (svc, db) = CreateService();
        var upload = CreateTestUpload();
        db.Set<PendingUpload>().Add(upload);
        await db.SaveChangesAsync();

        var request = new CaseCreationRequest
        {
            Parties = new() { new() { Role = "Debtor", Name = "D SRL", FiscalId = "RO1" } },
        };

        var result = await svc.CreateFromUploadAsync(upload, request);

        result.TasksCreated.Should().BeGreaterThan(0);

        var tasks = db.CompanyTasks.Where(t => t.CaseId == result.CaseId).ToList();
        tasks.Should().AllSatisfy(t =>
        {
        t.Deadline.Should().NotBeNull("every task must have a mandatory deadline");
  t.Status.Should().Be(TaskStatus.Open);
            t.AssignedToUserId.Should().Be(TestUserId);
        });

        // At least one critical task
        tasks.Should().Contain(t => t.IsCriticalDeadline);
    }

    // ?? Emails ??????????????????????????????????????????????

    [Fact]
 public async Task CreateFromUpload_SchedulesReminderEmails()
    {
        var (svc, db) = CreateService();
        var upload = CreateTestUpload();
    upload.DetectedClaimsDeadline = DateTime.UtcNow.AddDays(30);
        upload.DetectedNextHearingDate = DateTime.UtcNow.AddDays(14);
        db.Set<PendingUpload>().Add(upload);
   await db.SaveChangesAsync();

        var request = new CaseCreationRequest
        {
     Parties = new() { new() { Role = "Debtor", Name = "D SRL", FiscalId = "RO1" } },
        };

        var result = await svc.CreateFromUploadAsync(upload, request);

   result.EmailsScheduled.Should().BeGreaterThan(0);

 var emails = db.ScheduledEmails.Where(e => e.CaseId == result.CaseId).ToList();
        emails.Should().AllSatisfy(e =>
  {
       e.ScheduledFor.Should().BeAfter(DateTime.UtcNow);
e.To.Should().NotBeNullOrWhiteSpace();
       e.Subject.Should().Contain("Insolvex");
    });
    }

    // ?? Baseline deadlines ??????????????????????????????????

    [Fact]
    public async Task CreateFromUpload_ComputesBaselineDeadlines()
    {
        var (svc, db) = CreateService();
        var upload = CreateTestUpload();
        upload.DetectedClaimsDeadline = null; // force computation
      db.Set<PendingUpload>().Add(upload);
await db.SaveChangesAsync();

        var request = new CaseCreationRequest
        {
       NoticeDate = new DateTime(2025, 4, 1),
   Parties = new() { new() { Role = "Debtor", Name = "D SRL", FiscalId = "RO1" } },
      };

        var result = await svc.CreateFromUploadAsync(upload, request);

     result.BaselineDeadlines.Should().ContainKey("claimDeadline");
      result.BaselineDeadlines["claimDeadline"].Should().BeAfter(new DateTime(2025, 4, 1));

        // Case should have the computed deadline
        var caseEntity = db.InsolvencyCases.First(c => c.Id == result.CaseId);
        caseEntity.ClaimsDeadline.Should().NotBeNull();
        caseEntity.KeyDeadlinesJson.Should().NotBeNullOrWhiteSpace();
    }

    // ?? Phase mapping ???????????????????????????????????????

    [Fact]
    public void GetPhasesForProcedure_Faliment_StartsWithOpeningRequest()
    {
      var phases = CaseCreationService.GetPhasesForProcedure(ProcedureType.Faliment);

    phases.First().Should().Be(PhaseType.OpeningRequest);
      phases.Last().Should().Be(PhaseType.ProcedureClosure);
        phases.Should().Contain(PhaseType.ProcedureClosure);
    }

    [Fact]
    public void GetPhasesForProcedure_Reorganizare_IncludesReorgPhases()
  {
     var phases = CaseCreationService.GetPhasesForProcedure(ProcedureType.Reorganizare);

        phases.Should().Contain(PhaseType.ReorganizationPlanProposal);
     phases.Should().Contain(PhaseType.ReorganizationPlanVoting);
        phases.Should().Contain(PhaseType.ReorganizationExecution);
        phases.Should().NotContain(PhaseType.AssetLiquidation);
    }
}
