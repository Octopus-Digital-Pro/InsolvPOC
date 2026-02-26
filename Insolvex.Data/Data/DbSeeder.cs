using Microsoft.EntityFrameworkCore;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data;

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
      Status = "Active",
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

  // ── System template seeding ────────────────────────────────────────────────
  // Call this independently (idempotent) to ensure system templates always exist.

  public static async Task SeedSystemTemplatesAsync(ApplicationDbContext db)
  {
    static DocumentTemplate SystemTemplate(
      DocumentTemplateType type, string name, string stage, string category, string bodyHtml) =>
      new()
      {
        Id = Guid.NewGuid(),
        TenantId = null,    // global — visible to all tenants
        TemplateType = type,
        Name = name,
        FileName = "",
        StorageKey = "",
        ContentType = "text/html",
        IsSystem = true,
        IsActive = true,
        Stage = stage,
        Category = category,
        BodyHtml = bodyHtml,
        CreatedOn = DateTime.UtcNow,
        CreatedBy = "System",
      };

    var types = new[] {
      DocumentTemplateType.CreditorNotificationBpi,
      DocumentTemplateType.CreditorNotificationHtml,
      DocumentTemplateType.ReportArt97,
      DocumentTemplateType.PreliminaryClaimsTable,
      DocumentTemplateType.CreditorsMeetingMinutes,
      DocumentTemplateType.DefinitiveClaimsTable,
      DocumentTemplateType.FinalReportArt167,
    };

    // Only seed types that don't already exist as system templates
    var existingTypes = await db.DocumentTemplates
      .IgnoreQueryFilters()
      .Where(t => t.IsSystem)
      .Select(t => t.TemplateType)
      .ToListAsync();

    var toSeed = types.Where(t => !existingTypes.Contains(t)).ToList();
    if (toSeed.Count == 0) return;

    foreach (var type in toSeed)
    {
      var (name, stage, category, body) = type switch
      {
        DocumentTemplateType.CreditorNotificationBpi => (
          "Notificare creditori deschidere procedură (BPI)",
          "Deschidere procedură",
          "BPI",
          """
          <p>Către: <strong>{{RecipientName}}</strong></p>
          <p>{{RecipientAddress}}</p>
          <br/>
          <p>Referitor la dosarul nr. <strong>{{CaseNumber}}</strong> — {{DebtorName}}</p>
          <p>Prin prezenta vă notificăm că prin <strong>Sentința Civilă nr. {{OpeningDecisionNo}}</strong>
          din data de {{OpeningDate}}, {{CourtName}} a dispus deschiderea procedurii de insolvență
          față de debitorul <strong>{{DebtorName}}</strong> (CUI: {{DebtorCui}}).</p>
          <p>Termen depunere cereri de creanță: <strong>{{ClaimsDeadline}}</strong></p>
          <p>Termen contestații: <strong>{{ContestationsDeadline}}</strong></p>
          <br/>
          <p>Cu stimă,</p>
          <p><strong>{{PractitionerName}}</strong><br/>{{PractitionerRole}}<br/>{{PractitionerPhone}}</p>
          """
        ),
        DocumentTemplateType.CreditorNotificationHtml => (
          "Notificare deschidere procedură (HTML → PDF)",
          "Deschidere procedură",
          "Notificări",
          """
          <h2>NOTIFICARE DESCHIDERE PROCEDURĂ INSOLVENȚĂ</h2>
          <p>Dosar nr. <strong>{{CaseNumber}}</strong></p>
          <p>Debitor: <strong>{{DebtorName}}</strong>, CUI {{DebtorCui}}</p>
          <p>Instanță: {{CourtName}}, {{CourtSection}}</p>
          <p>Judecător sindic: {{JudgeSyndic}}</p>
          <hr/>
          <p>Stimați creditori,</p>
          <p>Prin prezenta vă notificăm deschiderea procedurii de insolvență.
          Vă rugăm să depuneți cererea de admitere a creanței până la data de
          <strong>{{ClaimsDeadline}}</strong>.</p>
          <p>Publicare BPI nr. {{BpiPublicationNo}} din {{BpiPublicationDate}}</p>
          <br/>
          <p>Administrator judiciar / Lichidator: <strong>{{PractitionerName}}</strong></p>
          <p>{{PractitionerEntityName}} — {{PractitionerAddress}}</p>
          """
        ),
        DocumentTemplateType.ReportArt97 => (
          "Raport 40 zile (Art. 97)",
          "Observație",
          "Rapoarte",
          """
          <h1>RAPORT PRIVIND CAUZELE ȘI ÎMPREJURĂRILE CARE AU DUS LA INSOLVENȚĂ</h1>
          <p>(conform Art. 97 din Legea 85/2014)</p>
          <p>Dosar nr. <strong>{{CaseNumber}}</strong> — <strong>{{DebtorName}}</strong></p>
          <p>Redactor: {{PractitionerName}}, {{PractitionerRole}}</p>
          <hr/>
          <h2>1. Date de identificare debitor</h2>
          <p>Denumire: {{DebtorName}}<br/>CUI: {{DebtorCui}}<br/>
          Sediu: {{DebtorAddress}}, {{DebtorLocality}}, jud. {{DebtorCounty}}</p>
          <h2>2. Situația economico-financiară</h2>
          <p>Total creanțe: <strong>{{TotalClaimsRon}} RON</strong></p>
          <p>Creanțe garantate: {{SecuredClaimsRon}} RON</p>
          <p>Creanțe chirografare: {{UnsecuredClaimsRon}} RON</p>
          <h2>3. Concluzii</h2>
          <p>[Completați concluziile raportului]</p>
          <br/>
          <p>Data: {{CurrentDate}}<br/>{{PractitionerName}}</p>
          """
        ),
        DocumentTemplateType.PreliminaryClaimsTable => (
          "Tabel preliminar de creanțe",
          "Verificare creanțe",
          "Tabele",
          """
          <h1>TABEL PRELIMINAR AL CREANȚELOR</h1>
          <p>Debitor: <strong>{{DebtorName}}</strong> — Dosar nr. {{CaseNumber}}</p>
          <p>Practician: {{PractitionerName}}</p>
          <hr/>
          <table border="1" width="100%" cellpadding="4">
            <thead>
              <tr>
                <th>#</th><th>Creditor</th><th>Tip creanță</th>
                <th>Suma solicitată (RON)</th><th>Suma acceptată (RON)</th><th>Observații</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>1</td><td>{{Creditor1_Name}}</td><td>{{Creditor1_Priority}}</td>
              <td>{{Creditor1_Amount}}</td><td></td><td></td></tr>
              <tr><td>2</td><td>{{Creditor2_Name}}</td><td>{{Creditor2_Priority}}</td>
              <td>{{Creditor2_Amount}}</td><td></td><td></td></tr>
            </tbody>
            <tfoot>
              <tr><td colspan="3"><strong>TOTAL</strong></td>
              <td colspan="3"><strong>{{TotalClaimsRon}} RON</strong></td></tr>
            </tfoot>
          </table>
          <br/>
          <p>Data: {{CurrentDate}}</p>
          """
        ),
        DocumentTemplateType.CreditorsMeetingMinutes => (
          "Proces-verbal AGC confirmare lichidator",
          "Adunarea creditorilor",
          "AGC",
          """
          <h1>PROCES-VERBAL</h1>
          <h2>Adunarea Generală a Creditorilor</h2>
          <p>Data: <strong>{{CreditorsMeetingDate}}</strong>, ora {{CreditorsMeetingTime}}</p>
          <p>Locație: {{CreditorsMeetingAddress}}</p>
          <p>Dosar: {{CaseNumber}} — {{DebtorName}}</p>
          <hr/>
          <h3>Participanți</h3>
          <p>[Completați lista participanților]</p>
          <h3>Ordine de zi</h3>
          <ol>
            <li>Confirmarea administratorului judiciar / lichidatorului judiciar</li>
            <li>Diverse</li>
          </ol>
          <h3>Hotărâri adoptate</h3>
          <p>Se confirmă în funcție de practician în insolvență <strong>{{PractitionerName}}</strong>,
          {{PractitionerRole}}, număr UNPIR {{PractitionerUNPIRNo}}.</p>
          <br/>
          <p>Secretar de ședință: [Completați]</p>
          <p>Data: {{CurrentDate}}</p>
          """
        ),
        DocumentTemplateType.DefinitiveClaimsTable => (
          "Tabel definitiv de creanțe",
          "Lichidare",
          "Tabele",
          """
          <h1>TABEL DEFINITIV AL CREANȚELOR</h1>
          <p>Debitor: <strong>{{DebtorName}}</strong> — Dosar nr. {{CaseNumber}}</p>
          <p>Practician: {{PractitionerName}}</p>
          <p>Aprobat de instanță la: [data]</p>
          <hr/>
          <table border="1" width="100%" cellpadding="4">
            <thead>
              <tr>
                <th>#</th><th>Creditor</th><th>Tip creanță</th>
                <th>Suma admisă (RON)</th><th>Rang</th>
              </tr>
            </thead>
            <tbody>
              <tr><td>1</td><td>{{Creditor1_Name}}</td><td>{{Creditor1_Priority}}</td>
              <td>{{Creditor1_Amount}}</td><td></td></tr>
              <tr><td>2</td><td>{{Creditor2_Name}}</td><td>{{Creditor2_Priority}}</td>
              <td>{{Creditor2_Amount}}</td><td></td></tr>
            </tbody>
            <tfoot>
              <tr><td colspan="3"><strong>TOTAL DEFINITIV</strong></td>
              <td colspan="2"><strong>{{TotalClaimsRon}} RON</strong></td></tr>
            </tfoot>
          </table>
          <br/>
          <p>Data: {{CurrentDate}}</p>
          """
        ),
        DocumentTemplateType.FinalReportArt167 => (
          "Raport final (Art. 167)",
          "Închidere",
          "Rapoarte",
          """
          <h1>RAPORT FINAL</h1>
          <p>(conform Art. 167 din Legea 85/2014)</p>
          <p>Dosar nr. <strong>{{CaseNumber}}</strong> — Debitor: <strong>{{DebtorName}}</strong></p>
          <p>Practician: {{PractitionerName}}, {{PractitionerRole}}</p>
          <hr/>
          <h2>1. Activitate desfășurată</h2>
          <p>[Completați descrierea activității]</p>
          <h2>2. Situația activelor și pasivelor la finalizare</h2>
          <p>Total creanțe admise: <strong>{{TotalClaimsRon}} RON</strong></p>
          <p>Creanțe garantate: {{SecuredClaimsRon}} RON</p>
          <p>Creanțe chirografare: {{UnsecuredClaimsRon}} RON</p>
          <h2>3. Distribuiri efectuate</h2>
          <p>[Completați tabelul distribuirilor]</p>
          <h2>4. Propunere de închidere a procedurii</h2>
          <p>Față de cele de mai sus, solicităm instanței să dispună închiderea procedurii
          de insolvență față de debitorul <strong>{{DebtorName}}</strong>.</p>
          <br/>
          <p>Data: {{CurrentDate}}<br/><strong>{{PractitionerName}}</strong><br/>{{PractitionerRole}}</p>
          """
        ),
        _ => ("", "", "", "")
      };

      if (string.IsNullOrEmpty(name)) continue;

      db.DocumentTemplates.Add(SystemTemplate(type, name, stage, category, body));
    }

    await db.SaveChangesAsync();
  }

  // ── Seed global workflow stage definitions ─────────────────────────────────

  /// <summary>
  /// Seeds the 8 global (TenantId = null) workflow stage definitions.
  /// These define the standard insolvency procedure flow.
  /// Tenants can create overrides per-stage via the API.
  /// </summary>
  public static async Task SeedWorkflowStagesAsync(ApplicationDbContext db)
  {
    if (await db.WorkflowStageDefinitions.IgnoreQueryFilters().AnyAsync(s => s.TenantId == null))
      return;

    var stages = new[]
    {
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "intake",
        Name = "Deschidere procedură",
        Description = "Înregistrare dosar, emitere notificări către debitor, creditori și instituții.",
        SortOrder = 0,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[\"CaseNumber\",\"DebtorName\",\"CourtName\",\"OpeningDate\"]",
        RequiredPartyRolesJson = "[\"Debtor\",\"InsolvencyPractitioner\"]",
        RequiredDocTypesJson = "[\"NotificareCreditori\",\"NotificareBPI\"]",
        OutputDocTypesJson = "[\"NotificareCreditori\",\"NotificareDebitor\",\"NotificareBPI\",\"NotificareORC\",\"NotificareANAF\"]",
        AllowedTransitionsJson = "[\"claims_collection\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "claims_collection",
        Name = "Colectare creanțe",
        Description = "Depunerea declarațiilor de creanță de către creditori. Verificare și analiză.",
        SortOrder = 1,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[\"ClaimsDeadline\"]",
        RequiredDocTypesJson = "[]",
        OutputDocTypesJson = "[]",
        AllowedTransitionsJson = "[\"preliminary_table\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "preliminary_table",
        Name = "Tabel preliminar de creanțe",
        Description = "Întocmirea și publicarea tabelului preliminar de creanțe.",
        SortOrder = 2,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[\"ContestationsDeadline\"]",
        RequiredDocTypesJson = "[\"TabelPreliminar\"]",
        OutputDocTypesJson = "[\"TabelPreliminar\"]",
        AllowedTransitionsJson = "[\"creditors_meeting\",\"definitive_table\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "creditors_meeting",
        Name = "Adunarea creditorilor",
        Description = "Convocare și desfășurare adunare creditori. Alegere comitet creditori.",
        SortOrder = 3,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[\"NextHearingDate\"]",
        RequiredDocTypesJson = "[\"ConvocareAdunareCreditori\"]",
        OutputDocTypesJson = "[\"ConvocareAdunareCreditori\",\"ProcesVerbalAdunare\"]",
        AllowedTransitionsJson = "[\"definitive_table\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "definitive_table",
        Name = "Tabel definitiv de creanțe",
        Description = "Soluționare contestații și publicare tabel definitiv.",
        SortOrder = 4,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[]",
        RequiredDocTypesJson = "[\"TabelDefinitiv\"]",
        OutputDocTypesJson = "[\"TabelDefinitiv\"]",
        AllowedTransitionsJson = "[\"causes_report\",\"asset_liquidation\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "causes_report",
        Name = "Raport cauze insolvență (40 zile)",
        Description = "Întocmire raport privind cauzele și împrejurările insolvenței în termen de 40 zile.",
        SortOrder = 5,
        ApplicableProcedureTypes = "Insolventa,Faliment,Reorganizare",
        RequiredFieldsJson = "[]",
        RequiredDocTypesJson = "[\"RaportCauze\"]",
        OutputDocTypesJson = "[\"RaportCauze\"]",
        AllowedTransitionsJson = "[\"asset_liquidation\",\"final_report\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "asset_liquidation",
        Name = "Lichidare active",
        Description = "Identificare, evaluare și valorificare bunuri din averea debitoarei.",
        SortOrder = 6,
        ApplicableProcedureTypes = "Faliment,FalimentSimplificat",
        RequiredFieldsJson = "[]",
        RequiredDocTypesJson = "[]",
        OutputDocTypesJson = "[\"RaportDistributie\"]",
        AllowedTransitionsJson = "[\"final_report\"]",
        IsActive = true,
      },
      new WorkflowStageDefinition
      {
        TenantId = null,
        StageKey = "final_report",
        Name = "Raport final și închidere procedură",
        Description = "Întocmire raport final, distribuire sume, cerere de închidere procedură.",
        SortOrder = 7,
        ApplicableProcedureTypes = "Insolventa,Faliment,FalimentSimplificat,Reorganizare",
        RequiredFieldsJson = "[]",
        RequiredDocTypesJson = "[\"RaportFinal\"]",
        OutputDocTypesJson = "[\"RaportFinal\"]",
        AllowedTransitionsJson = "[]",
        IsActive = true,
      },
    };

    db.WorkflowStageDefinitions.AddRange(stages);
    await db.SaveChangesAsync();
  }
}
