using Microsoft.EntityFrameworkCore;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Data;

public static class DbSeeder
{
  public static async Task SeedAsync(ApplicationDbContext db)
  {
    if (await db.Tenants.AnyAsync())
      return;

    // Default tenant
    var tenantId = Guid.NewGuid();
    var tenant = new Tenant
    {
      Id = tenantId,
      Name = "Demo Firm",
      Domain = "demo.insolvex.local",
      IsActive = true,
      PlanName = "Professional",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Tenants.Add(tenant);

    // Insolvency Firm (licensee)
    var firmId = Guid.NewGuid();
    db.InsolvencyFirms.Add(new InsolvencyFirm
    {
      Id = firmId,
      TenantId = tenantId,
      FirmName = "Cabinet Insolvex IPURL",
      CuiRo = "RO99887766",
      TradeRegisterNo = "J12/9999/2020",
      VatNumber = "RO99887766",
      UnpirRegistrationNo = "RFO II-0999",
      Address = "Str. Avram Iancu nr. 10, Et. 2",
      Locality = "Cluj-Napoca",
      County = "Cluj",
      Country = "Romania",
      PostalCode = "400000",
      Phone = "+40 264 111 222",
      Email = "office@insolvex.local",
      Website = "https://insolvex.local",
      ContactPerson = "Admin User",
      Iban = "RO49AAAA1B31007593840000",
      BankName = "Banca Transilvania",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    });

    // Global admin
    var adminId = Guid.NewGuid();
    var admin = new User
    {
      Id = adminId,
      TenantId = tenantId,
      Email = "admin@insolvex.local",
      FirstName = "Admin",
      LastName = "User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", 12),
      Role = UserRole.GlobalAdmin,
      IsActive = true,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Users.Add(admin);

    // Practitioner user
    var practitionerId = Guid.NewGuid();
    var practitioner = new User
    {
      Id = practitionerId,
      TenantId = tenantId,
      Email = "practitioner@insolvex.local",
      FirstName = "Jon",
      LastName = "Doe",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pract123!", 12),
      Role = UserRole.Practitioner,
      IsActive = true,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Users.Add(practitioner);

    // Secretary user
    var secretaryId = Guid.NewGuid();
    var secretary = new User
    {
      Id = secretaryId,
      TenantId = tenantId,
      Email = "secretary@insolvex.local",
      FirstName = "Gipsz",
      LastName = "Jakab",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Secr123!", 12),
      Role = UserRole.Secretary,
      IsActive = true,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Users.Add(secretary);

    // Demo debtor company
    var debtorCompanyId = Guid.NewGuid();
    var debtorCompany = new Company
    {
      Id = debtorCompanyId,
      TenantId = tenantId,
      Name = "SC Example Debtor SRL",
      CompanyType = CompanyType.Debtor,
      CuiRo = "RO12345678",
      TradeRegisterNo = "J12/1234/2018",
      Address = "Str. Exemplu nr. 1",
      County = "Cluj",
      Locality = "Cluj-Napoca",
      Country = "Romania",
      Caen = "4120",
      IncorporationYear = "2018",
      AssignedToUserId = practitionerId,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Companies.Add(debtorCompany);

    // Demo creditor company
    var creditorCompanyId = Guid.NewGuid();
    db.Companies.Add(new Company
    {
      Id = creditorCompanyId,
      TenantId = tenantId,
      Name = "SC Furnizor Total SA",
      CompanyType = CompanyType.Creditor,
      CuiRo = "RO55667788",
      Address = "Bd. Independentei nr. 55",
      County = "Cluj",
      Locality = "Cluj-Napoca",
      Country = "Romania",
      Phone = "+40 264 333 444",
      Email = "contact@furnizor.ro",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    });

    // Demo practitioner company (the firm itself as a company)
    var practFirmCompanyId = Guid.NewGuid();
    db.Companies.Add(new Company
    {
      Id = practFirmCompanyId,
      TenantId = tenantId,
      Name = "Cabinet Insolvex IPURL",
      CompanyType = CompanyType.InsolvencyPractitioner,
      CuiRo = "RO99887766",
      TradeRegisterNo = "J12/9999/2020",
      Address = "Str. Avram Iancu nr. 10, Et. 2",
      County = "Cluj",
      Locality = "Cluj-Napoca",
      Country = "Romania",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    });

    // Demo case
    var caseId = Guid.NewGuid();
    var insolvencyCase = new InsolvencyCase
    {
      Id = caseId,
      TenantId = tenantId,
      CaseNumber = "1234/1285/2025",
      CourtName = "Tribunalul Cluj",
      CourtSection = "Sectia a II-a Civila",
      JudgeSyndic = "Pop Maria",
      DebtorName = "SC Example Debtor SRL",
      DebtorCui = "RO12345678",
      ProcedureType = ProcedureType.FalimentSimplificat,
      Stage = CaseStage.EligibilitySetup,
      LawReference = "Legea 85/2014",
      PractitionerName = "Cabinet Insolvex IPURL",
      PractitionerRole = "lichidator_judiciar",
      PractitionerFiscalId = "RO99887766",
      CompanyId = debtorCompanyId,
      AssignedToUserId = practitionerId,
      OpeningDate = DateTime.UtcNow.AddDays(-30),
      NextHearingDate = DateTime.UtcNow.AddDays(14),
      ClaimsDeadline = DateTime.UtcNow.AddDays(45),
      BpiPublicationNo = "BPI 12345/2025",
      BpiPublicationDate = DateTime.UtcNow.AddDays(-28),
      OpeningDecisionNo = "Sent. Civ. 999/2025",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.InsolvencyCases.Add(insolvencyCase);

    // Case parties
    db.CaseParties.AddRange(
            new CaseParty
            {
              Id = Guid.NewGuid(),
              TenantId = tenantId,
              CaseId = caseId,
              CompanyId = debtorCompanyId,
              Role = CasePartyRole.Debtor,
              JoinedDate = DateTime.UtcNow.AddDays(-30),
              CreatedOn = DateTime.UtcNow,
              CreatedBy = "System"
            },
       new CaseParty
       {
         Id = Guid.NewGuid(),
         TenantId = tenantId,
         CaseId = caseId,
         CompanyId = practFirmCompanyId,
         Role = CasePartyRole.InsolvencyPractitioner,
         RoleDescription = "Lichidator judiciar",
         JoinedDate = DateTime.UtcNow.AddDays(-30),
         CreatedOn = DateTime.UtcNow,
         CreatedBy = "System"
       },
       new CaseParty
       {
         Id = Guid.NewGuid(),
         TenantId = tenantId,
         CaseId = caseId,
         CompanyId = creditorCompanyId,
         Role = CasePartyRole.UnsecuredCreditor,
         RoleDescription = "Creditor chirografar",
         ClaimAmountRon = 125000.00m,
         ClaimAccepted = true,
         JoinedDate = DateTime.UtcNow.AddDays(-15),
         CreatedOn = DateTime.UtcNow,
         CreatedBy = "System"
       }
          );

    // Case phases (simplified bankruptcy workflow)
    db.CasePhases.AddRange(
 new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.OpeningRequest, Status = PhaseStatus.Completed, SortOrder = 1, StartedOn = DateTime.UtcNow.AddDays(-35), CompletedOn = DateTime.UtcNow.AddDays(-30), CourtDecisionRef = "Sent. Civ. 999/2025", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
        new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.CreditorNotification, Status = PhaseStatus.Completed, SortOrder = 2, StartedOn = DateTime.UtcNow.AddDays(-30), CompletedOn = DateTime.UtcNow.AddDays(-25), CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
 new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.ClaimsFiling, Status = PhaseStatus.InProgress, SortOrder = 3, StartedOn = DateTime.UtcNow.AddDays(-25), DueDate = DateTime.UtcNow.AddDays(45), CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
 new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.PreliminaryClaimsTable, Status = PhaseStatus.NotStarted, SortOrder = 4, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
    new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.ClaimsContestations, Status = PhaseStatus.NotStarted, SortOrder = 5, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.DefinitiveClaimsTable, Status = PhaseStatus.NotStarted, SortOrder = 6, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
        new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.AssetLiquidation, Status = PhaseStatus.NotStarted, SortOrder = 7, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
 new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.CreditorDistribution, Status = PhaseStatus.NotStarted, SortOrder = 8, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.FinalReport, Status = PhaseStatus.NotStarted, SortOrder = 9, CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
 new CasePhase { Id = Guid.NewGuid(), TenantId = tenantId, CaseId = caseId, PhaseType = PhaseType.ProcedureClosure, Status = PhaseStatus.NotStarted, SortOrder = 10, CreatedOn = DateTime.UtcNow, CreatedBy = "System" }
);

    // Demo tasks
    db.CompanyTasks.AddRange(
     new CompanyTask
     {
       Id = Guid.NewGuid(),
       TenantId = tenantId,
       CompanyId = debtorCompanyId,
       Title = "File opening notification to ONRC",
       Description = "Notify trade register of insolvency opening",
       Deadline = DateTime.UtcNow.AddDays(7),
       Status = Domain.Enums.TaskStatus.Open,
       AssignedToUserId = practitionerId,
       CreatedOn = DateTime.UtcNow,
       CreatedBy = "System"
     },
             new CompanyTask
             {
               Id = Guid.NewGuid(),
               TenantId = tenantId,
               CompanyId = debtorCompanyId,
               Title = "Prepare Art. 97 Report",
               Description = "Draft the initial report on causes of insolvency",
               Labels = "report, urgent",
               Deadline = DateTime.UtcNow.AddDays(21),
               Status = Domain.Enums.TaskStatus.Open,
               AssignedToUserId = practitionerId,
               CreatedOn = DateTime.UtcNow,
               CreatedBy = "System"
             },
    new CompanyTask
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      CompanyId = debtorCompanyId,
      Title = "Publish opening in BPI",
      Deadline = DateTime.UtcNow.AddDays(-2),
      Status = Domain.Enums.TaskStatus.Done,
      AssignedToUserId = secretaryId,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    }
         );

    // System configuration defaults
    db.Set<SystemConfig>().AddRange(
    new SystemConfig
    {
      Id = Guid.NewGuid(),
      Key = "StorageProvider",
      Value = "Local",
      Description = "File storage provider: Local or AwsS3",
      Group = "Storage",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    },
   new SystemConfig
   {
     Id = Guid.NewGuid(),
     Key = "S3:AccessKeyId",
     Value = "",
     Description = "AWS S3 Access Key ID",
     Group = "Storage",
     CreatedOn = DateTime.UtcNow,
     CreatedBy = "System"
   },
 new SystemConfig
 {
   Id = Guid.NewGuid(),
   Key = "S3:SecretAccessKey",
   Value = "",
   Description = "AWS S3 Secret Access Key",
   Group = "Storage",
   CreatedOn = DateTime.UtcNow,
   CreatedBy = "System"
 },
   new SystemConfig
   {
     Id = Guid.NewGuid(),
     Key = "S3:Region",
     Value = "eu-central-1",
     Description = "AWS S3 Region",
     Group = "Storage",
     CreatedOn = DateTime.UtcNow,
     CreatedBy = "System"
   },
new SystemConfig
{
Id = Guid.NewGuid(),
Key = "S3:BucketName",
Value = "",
Description = "AWS S3 Bucket Name",
Group = "Storage",
CreatedOn = DateTime.UtcNow,
CreatedBy = "System"
},
        new SystemConfig
        {
          Id = Guid.NewGuid(),
          Key = "S3:KeyPrefix",
          Value = "documents/",
          Description = "AWS S3 key prefix (folder)",
          Group = "Storage",
          CreatedOn = DateTime.UtcNow,
          CreatedBy = "System"
        },
        new SystemConfig
        {
          Id = Guid.NewGuid(),
          Key = "S3:ServiceUrl",
          Value = "",
          Description = "Custom S3 endpoint URL (for MinIO/LocalStack)",
          Group = "Storage",
          CreatedOn = DateTime.UtcNow,
          CreatedBy = "System"
        },
        new SystemConfig
        {
          Id = Guid.NewGuid(),
          Key = "S3:ForcePathStyle",
          Value = "false",
          Description = "Force path-style addressing for S3-compatible services",
          Group = "Storage",
          CreatedOn = DateTime.UtcNow,
          CreatedBy = "System"
        },
new SystemConfig
{
Id = Guid.NewGuid(),
Key = "DefaultClaimDeadlineDays",
Value = "45",
Description = "Default number of days from NoticeDate for claims deadline",
Group = "Deadlines",
CreatedOn = DateTime.UtcNow,
CreatedBy = "System"
},
new SystemConfig
{
  Id = Guid.NewGuid(),
  Key = "DefaultContestationDeadlineDays",
  Value = "5",
  Description = "Default days after claims deadline for contestation period",
  Group = "Deadlines",
  CreatedOn = DateTime.UtcNow,
  CreatedBy = "System"
},
    new SystemConfig
    {
      Id = Guid.NewGuid(),
      Key = "DefaultNotificationDays",
      Value = "3",
      Description = "Default days from case creation to send initial notifications",
      Group = "Deadlines",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    },
      // Deadline engine settings (per InsolvencyAppRules)
      new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:ClaimDeadlineDaysFromNotice", Value = "30", Description = "Days from NoticeDate for claim submission deadline", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:ObjectionDeadlineDaysFromNotice", Value = "45", Description = "Days from NoticeDate for objection deadline", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
   new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:SendInitialNoticeWithinDays", Value = "2", Description = "Days from NoticeDate to send initial notice", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
        new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:MeetingNoticeMinimumDays", Value = "14", Description = "Minimum days before meeting to send notices", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
   new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:ReportEveryNDays", Value = "30", Description = "Periodic report interval in days", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:UseBusinessDays", Value = "false", Description = "Use business days instead of calendar days", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
  new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:AdjustToNextWorkingDay", Value = "true", Description = "Adjust deadline to next working day if it falls on weekend/holiday", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" },
new SystemConfig { Id = Guid.NewGuid(), Key = "Deadlines:ReminderDays", Value = "7,3,1,0", Description = "Days before deadline to send reminders (comma-separated)", Group = "Deadlines", CreatedOn = DateTime.UtcNow, CreatedBy = "System" }
    );

    // Tenant-specific deadline settings (per InsolvencyAppRules)
    db.TenantDeadlineSettings.Add(new TenantDeadlineSettings
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      SendInitialNoticeWithinDays = 2,
      ClaimDeadlineDaysFromNotice = 30,
      ObjectionDeadlineDaysFromNotice = 45,
      MeetingNoticeMinimumDays = 14,
      ReportEveryNDays = 30,
      UseBusinessDays = false,
      AdjustToNextWorkingDay = true,
      ReminderDaysBeforeDeadline = "7,3,1,0",
      UrgentQueueHoursBeforeDeadline = 24,
      AutoAssignBackupOnCriticalOverdue = false,
      EmailFromName = "Insolvex Notifications",
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    });

    await db.SaveChangesAsync();
  }
}
