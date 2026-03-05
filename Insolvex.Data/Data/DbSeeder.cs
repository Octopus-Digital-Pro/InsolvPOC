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

    // Tenant admin user
    var tenantAdminId = Guid.NewGuid();
    var tenantAdmin = new User
    {
      Id = tenantAdminId,
      TenantId = tenantId,
      Email = "tenantadmin@insolvex.local",
      FirstName = "Tenant",
      LastName = "Admin",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("TAdmin123!", 12),
      Role = UserRole.TenantAdmin,
      IsActive = true,
      CreatedOn = DateTime.UtcNow,
      CreatedBy = "System"
    };
    db.Users.Add(tenantAdmin);

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
      DocumentTemplateType.MandatoryReport,
      DocumentTemplateType.PreliminaryClaimsTable,
      DocumentTemplateType.CreditorsMeetingMinutes,
      DocumentTemplateType.DefinitiveClaimsTable,
      DocumentTemplateType.FinalReportArt167,
    };

    // Only seed types that don't already exist as system templates
    var existingTemplates = await db.DocumentTemplates
      .IgnoreQueryFilters()
      .Where(t => t.IsSystem)
      .ToListAsync();
    var existingByType = existingTemplates.ToDictionary(t => t.TemplateType);

    bool changed = false;
    foreach (var type in types)
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
        DocumentTemplateType.MandatoryReport => (
          "Raport periodic obligatoriu (30 zile)",
          "Monitorizare",
          "Rapoarte",
          """
          <h1>RAPORT PERIODIC OBLIGATORIU</h1>
          <p>(raport la fiecare 30 zile, conform setărilor de termene ale tenantului)</p>
          <p>Dosar nr. <strong>{{CaseNumber}}</strong> — Debitor: <strong>{{DebtorName}}</strong></p>
          <p>Instanță: {{CourtName}} {{CourtSection}}</p>
          <p>Practician: {{PractitionerName}} ({{PractitionerRole}})</p>
          <hr/>
          <h2>1. Situația generală a procedurii</h2>
          <p>[Completați evoluția procedurii din ultimele 30 zile]</p>

          <h2>2. Activități realizate în perioada de raportare</h2>
          <p>Perioadă analizată: <strong>{{PastTasksFromDate}}</strong> – <strong>{{PastTasksToDate}}</strong></p>
          {{#if HasPastReportedTasks}}
            {{{PastTasksSummaryWithReportHtml}}}
          {{else}}
            <p>Nu există task-uri cu rezumat de raport în această perioadă (status Done / InProgress).</p>
          {{/if}}

          <h2>3. Situația creanțelor și a activelor</h2>
          <p>Total creanțe: <strong>{{TotalClaimsRon}} RON</strong></p>
          <p>Creanțe garantate: {{SecuredClaimsRon}} RON</p>
          <p>Creanțe chirografare: {{UnsecuredClaimsRon}} RON</p>
          <p>Valoare estimată active: {{EstimatedAssetValueRon}} RON</p>

          <h2>4. Task-uri planificate pentru următoarea perioadă</h2>
          <p>Perioadă analizată: <strong>{{FutureTasksFromDate}}</strong> – <strong>{{FutureTasksToDate}}</strong></p>
          {{#if HasFuturePlannedTasks}}
            {{{FutureTasksNamesHtml}}}
          {{else}}
            <p>Nu există task-uri nefinalizate cu deadline în perioada selectată.</p>
          {{/if}}

          <br/>
          <p>Data raportului: {{CurrentDate}}</p>
          <p>Practician în insolvență: <strong>{{PractitionerName}}</strong></p>
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

      if (!existingByType.TryGetValue(type, out var existingTpl))
      {
        db.DocumentTemplates.Add(SystemTemplate(type, name, stage, category, body));
        changed = true;
      }
      else if (string.IsNullOrWhiteSpace(existingTpl.BodyHtml))
      {
        existingTpl.BodyHtml = body;
        existingTpl.IsActive = true;
        changed = true;
      }
    }

    if (changed) await db.SaveChangesAsync();
  }

  // ── Ensure demo users always exist (idempotent, safe on existing DBs) ────────
  /// <summary>
  /// Creates the four demo login accounts if any of them are missing.
  /// Safe to call on every startup — checks each user by email before inserting.
  /// </summary>
  public static async Task EnsureDemoUsersAsync(ApplicationDbContext db)
  {
    // Demo users require a tenant to exist (SeedAsync must have run first).
    var tenant = await db.Tenants.IgnoreQueryFilters()
        .OrderBy(t => t.CreatedOn)
        .FirstOrDefaultAsync();
    if (tenant == null) return;

    var now = DateTime.UtcNow;

    var demoUsers = new[]
    {
      new { Email = "admin@insolvex.local",          Password = "Admin123!",   First = "Admin",  Last = "User",  Role = UserRole.GlobalAdmin  },
      new { Email = "tenantadmin@insolvex.local",    Password = "TAdmin123!",  First = "Tenant", Last = "Admin", Role = UserRole.TenantAdmin  },
      new { Email = "practitioner@insolvex.local",   Password = "Pract123!",   First = "Jon",    Last = "Doe",   Role = UserRole.Practitioner },
      new { Email = "secretary@insolvex.local",      Password = "Secr123!",    First = "Gipsz",  Last = "Jakab", Role = UserRole.Secretary    },
    };

    bool changed = false;
    foreach (var u in demoUsers)
    {
      var exists = await db.Users.IgnoreQueryFilters()
          .AnyAsync(x => x.Email == u.Email);
      if (exists) continue;

      db.Users.Add(new User
      {
        Id         = Guid.NewGuid(),
        TenantId   = tenant.Id,
        Email      = u.Email,
        FirstName  = u.First,
        LastName   = u.Last,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(u.Password, 12),
        Role       = u.Role,
        IsActive   = true,
        CreatedOn  = now,
        CreatedBy  = "System",
      });
      changed = true;
    }

    if (changed) await db.SaveChangesAsync();
  }

  // ── Seed global workflow stage definitions ─────────────────────────────────

  /// <summary>
  /// Seeds the global (TenantId = null) workflow stage definitions for all Romanian
  /// insolvency procedure types defined in Legea 85/2014.
  ///
  /// Procedure types and their stage sets:
  ///   • Insolventa            – observation → claims → preliminary/definitive table → final
  ///   • Faliment              – same as Insolventa + asset inventory/valuation/liquidation/distribution
  ///   • FalimentSimplificat   – no observation, no Art.97 report + asset liquidation path
  ///   • Reorganizare          – all common stages + full reorganization plan cycle (Art. 133-142)
  ///   • ConcordatPreventiv    – preventive concordat lifecycle (Art. 31-50)
  ///   • MandatAdHoc           – ad-hoc mandate negotiation (Art. 21-30)
  ///
  /// Stage resolution: when a case's workflow is initialised, only stages whose
  /// ApplicableProcedureTypes includes the case's ProcedureType are used.
  /// Tenants can create overrides per StageKey via the Workflow Stages admin page.
  /// </summary>
  public static async Task SeedWorkflowStagesAsync(ApplicationDbContext db)
  {
    // Upsert by StageKey — seed any global stage that is missing.
    // This is resilient to partial states (e.g. earlier crash mid-seed).
    var existingKeys = (await db.WorkflowStageDefinitions
      .IgnoreQueryFilters()
      .Where(s => s.TenantId == null)
      .Select(s => s.StageKey)
      .ToListAsync())
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // ── Local helpers ──────────────────────────────────────────────────────
    static string J(params string[] items) =>
      "[" + string.Join(",", items.Select(i => $"\"{i}\"")) + "]";

    static string T(params (string title, string desc, int days, string cat)[] tasks) =>
      "[" + string.Join(",", tasks.Select(t =>
        $"{{\"title\":\"{t.title}\",\"description\":\"{t.desc}\",\"deadlineDays\":{t.days},\"category\":\"{t.cat}\"}}")) + "]";

    // Common applicable-procedure-type groups
    const string ALL4  = "Insolventa,Faliment,FalimentSimplificat,Reorganizare";
    const string NO_FS = "Insolventa,Faliment,Reorganizare";      // not FalimentSimplificat
    const string FS_F  = "Faliment,FalimentSimplificat";           // liquidation-path only
    const string REORG = "Reorganizare";
    const string CONC  = "ConcordatPreventiv";
    const string MAND  = "MandatAdHoc";

    var now = DateTime.UtcNow;

    var stages = new List<WorkflowStageDefinition>
    {
      // ════════════════════════════════════════════════════════════════════
      // BLOC 1: ETAPE COMUNE  (Insolventa / Faliment / FalimentSimplificat / Reorganizare)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "intake",
        Name = "Deschidere procedură",
        Description = "Înregistrare dosar, emitere notificări obligatorii (creditori, BPI, ONRC, ANAF), " +
                      "numire/confirmare practician în insolvență. Art. 66-70 Legea 85/2014.",
        SortOrder = 10,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = J("CaseNumber","DebtorName","CourtName","OpeningDate"),
        RequiredPartyRolesJson   = J("Debtor","InsolvencyPractitioner"),
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("NotificareCreditori","NotificareDebitor","NotificareBPI","NotificareORC","NotificareANAF"),
        OutputTasksJson          = T(
          ("Notifică ONRC de deschiderea procedurii","Depune cererea la Oficiul Registrului Comerțului în termen de 5 zile",5,"Filing"),
          ("Publică notificarea în BPI","Publicarea în Buletinul Procedurilor de Insolvență (BPI)",5,"Filing"),
          ("Trimite notificări creditorilor cunoscuți","Notifică toți creditorii înscriși în evidențele debitorului",10,"Email"),
          ("Notifică ANAF de deschiderea procedurii","Transmite notificarea la administrația fiscală competentă",5,"Filing"),
          ("Solicită extrase CF și certificate ONRC pentru toate bunurile","Identificare imobile și participații la societăți",10,"Document"),
          ("Solicită cazierul fiscal al debitorului de la ANAF","Obținerea cazierului fiscal",7,"Compliance")
        ),
        AllowedTransitionsJson   = J("observation","claims_collection"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "observation",
        Name = "Perioadă de observație",
        Description = "Debitorul continuă activitatea sub supravegherea practicianului (Art. 67-69). " +
                      "Inventar provizoriu, raport de activitate, evaluare posibilitate de reorganizare. " +
                      "Nu se aplică falimentului simplificat (Art. 38 – procedură direct la faliment).",
        SortOrder = 20,
        ApplicableProcedureTypes = NO_FS,
        RequiredFieldsJson       = J("NoticeDate"),
        RequiredPartyRolesJson   = J("Debtor","InsolvencyPractitioner"),
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Întocmește inventarul provizoriu al averii debitorului","Înregistrarea bunurilor mobile, imobile și creanțelor",20,"Document"),
          ("Redactează raportul de activitate al perioadei de observație","Situația activității debitoarei, riscuri identificate",30,"Report"),
          ("Verifică continuarea / suspendarea activității debitorului","Decizia privind gestionarea curentă a afacerii",14,"Review"),
          ("Solicită și verifică bilanțul de intrare în insolvență","Bilanț contabil la data deschiderii procedurii",15,"Document")
        ),
        AllowedTransitionsJson   = J("claims_collection"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "claims_collection",
        Name = "Colectare declarații de creanță",
        Description = "Creditorii depun declarațiile de creanță în termenul stabilit prin sentința de deschidere " +
                      "(Art. 104-110 Legea 85/2014). Termen standard: 30 de zile de la NoticeDate.",
        SortOrder = 30,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = J("ClaimsDeadline"),
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("NotificareCreditori"),
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Înregistrează și verifică declarațiile de creanță primite","Procesarea fiecărei cereri de admitere a creanței",30,"Review"),
          ("Solicită documente suplimentare creditorilor cu creanțe incomplete","Cereri de completare documente doveditoare",15,"Email"),
          ("Centralizează lista creanțelor verificate","Pregătire date pentru tabelul preliminar",5,"Document")
        ),
        AllowedTransitionsJson   = J("causes_report","preliminary_table"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "causes_report",
        Name = "Raport cauze insolvență (Art. 97 / 40 zile)",
        Description = "Administratorul/lichidatorul judiciar întocmește raportul privind cauzele și împrejurările " +
                      "care au dus la insolvență (Art. 97 Legea 85/2014). Termen: 40 de zile de la deschidere. " +
                      "Obligatoriu pentru Insolventa, Faliment, Reorganizare; opțional pentru Faliment Simplificat.",
        SortOrder = 40,
        ApplicableProcedureTypes = NO_FS,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("RaportCauze"),
        OutputTasksJson          = T(
          ("Redactează raportul privind cauzele insolvenței (Art. 97)","Analiză cauze, împrejurări, persoane responsabile",40,"Report"),
          ("Depune raportul Art. 97 la judecătorul sindic","Depunere la dosarul cauzei în termen de 40 zile",40,"Filing"),
          ("Publică raportul Art. 97 în BPI","Publicare în Buletinul Procedurilor de Insolvență",42,"Filing"),
          ("Identifică eventuale acțiuni de răspundere patrimonială (Art. 169)","Analiza posibilității atragerii răspunderii personale",45,"Compliance")
        ),
        AllowedTransitionsJson   = J("preliminary_table"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "preliminary_table",
        Name = "Tabel preliminar de creanțe",
        Description = "Întocmirea tabelului preliminar cu creanțele acceptate/contestate (Art. 111-113 Legea 85/2014). " +
                      "Se publică în BPI și se afișează la tribunal. Termen de contestație: 5 zile.",
        SortOrder = 50,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = J("ContestationsDeadline"),
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("TabelPreliminar"),
        OutputTasksJson          = T(
          ("Întocmește tabelul preliminar de creanțe (Art. 111)","Verificarea și înscrierea creanțelor acceptate și respinse",14,"Document"),
          ("Depune tabelul preliminar la judecătorul sindic","Depunere la dosarul cauzei",14,"Filing"),
          ("Publică tabelul preliminar în BPI","Publicare oficială pentru a permite contestațiile",14,"Filing"),
          ("Afișează tabelul la sediul tribunalului","Afișare fizică la dosarul cauzei",14,"Compliance")
        ),
        AllowedTransitionsJson   = J("contestations"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "contestations",
        Name = "Soluționare contestații tabel preliminar",
        Description = "Termenul de 5 zile pentru depunerea contestațiilor la tabelul preliminar (Art. 113-114). " +
                      "Judecătorul sindic soluționează contestațiile. Ulterior se publică tabelul rectificat.",
        SortOrder = 60,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("TabelPreliminar"),
        OutputDocTypesJson       = J("TabelPreliminarRectificat"),
        OutputTasksJson          = T(
          ("Înregistrează contestațiile depuse la tabelul preliminar","Procesarea contestațiilor primite",5,"Filing"),
          ("Redactează întâmpinările la contestațiile fondate","Răspuns la contestațiile admisibile",10,"Document"),
          ("Participă la ședința de soluționare contestații","Reprezentare la dosarul cauzei",14,"Meeting"),
          ("Rectifică tabelul conform hotărârii judecătorului sindic","Actualizarea tabelului după sentință",7,"Document")
        ),
        AllowedTransitionsJson   = J("creditors_meeting","definitive_table"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "creditors_meeting",
        Name = "Adunarea generală a creditorilor (AGC)",
        Description = "Convocare și desfășurare adunare generală a creditorilor (Art. 78-88 Legea 85/2014). " +
                      "Alegere comitet creditori, confirmare practician, vot plan (dacă există). " +
                      "Convocatoarele se trimit cu minim 14 zile înainte.",
        SortOrder = 70,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = J("Debtor","InsolvencyPractitioner"),
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("ConvocareAGC","ProcesVerbalAGC"),
        OutputTasksJson          = T(
          ("Convoaca adunarea creditorilor (min. 14 zile înainte)","Transmitere convocatoare prin poștă recomandată și email",14,"Email"),
          ("Publică convocatoarea în BPI","Publicare formală în Buletinul Procedurilor de Insolvență",14,"Filing"),
          ("Organizează și desfășoară adunarea creditorilor","Prezidarea ședinței, votul ordinii de zi",0,"Meeting"),
          ("Întocmește procesul-verbal al adunării","Redactarea PV cu participanți, hotărâri adoptate",2,"Document"),
          ("Publică procesul-verbal în BPI","Publicare PV în termen de 3 zile",3,"Filing")
        ),
        AllowedTransitionsJson   = J("definitive_table"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "definitive_table",
        Name = "Tabel definitiv de creanțe",
        Description = "Întocmirea tabelului definitiv după soluționarea tuturor contestațiilor (Art. 122 Legea 85/2014). " +
                      "Tabelul definitiv stabilește ordinea de prioritate a creanțelor pentru distribuire.",
        SortOrder = 80,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("ProcesVerbalAGC"),
        OutputDocTypesJson       = J("TabelDefinitiv"),
        OutputTasksJson          = T(
          ("Întocmește tabelul definitiv de creanțe (Art. 122)","Finalizare după soluționarea tuturor contestațiilor",7,"Document"),
          ("Depune tabelul definitiv la judecătorul sindic","Depunere la dosarul cauzei",7,"Filing"),
          ("Publică tabelul definitiv în BPI","Publicare oficială în BPI",7,"Filing")
        ),
        AllowedTransitionsJson   = J("plan_elaboration","asset_inventory","final_report"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      // ════════════════════════════════════════════════════════════════════
      // BLOC 2: PLAN DE REORGANIZARE  (Reorganizare – Art. 133-142)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "plan_elaboration",
        Name = "Elaborare plan de reorganizare",
        Description = "Administratorul judiciar, debitorul sau un creditor cu min. 20% din creanțe " +
                      "elaborează planul de reorganizare (Art. 133-135 Legea 85/2014). " +
                      "Termen maxim: 30 de zile de la publicarea tabelului definitiv.",
        SortOrder = 90,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = J("ReorganizationPlanDeadline"),
        RequiredPartyRolesJson   = J("Debtor","InsolvencyPractitioner"),
        RequiredDocTypesJson     = J("TabelDefinitiv"),
        OutputDocTypesJson       = J("PlanReorganizare"),
        OutputTasksJson          = T(
          ("Redactează proiectul planului de reorganizare (Art. 133)","Cuprinde masuri de restructurare, termene, creditori afectați",30,"Document"),
          ("Consultații cu creditorii principali asupra termenilor planului","Negociere clauze plan, obținere acorduri preliminare",25,"Meeting"),
          ("Depune planul de reorganizare la judecătorul sindic","Depunere oficială la dosarul cauzei",30,"Filing")
        ),
        AllowedTransitionsJson   = J("plan_admission"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "plan_admission",
        Name = "Admitere plan de reorganizare",
        Description = "Judecătorul sindic examinează și admite sau respinge planul de reorganizare (Art. 136-137). " +
                      "Admiterea face planul accesibil votului creditorilor.",
        SortOrder = 100,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("PlanReorganizare"),
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Obține sentința de admitere a planului de reorganizare","Urmărire dosar, participare termen admitere",14,"Filing"),
          ("Publică planul admis în BPI","Publicare pentru informarea creditorilor",7,"Filing")
        ),
        AllowedTransitionsJson   = J("plan_vote"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "plan_vote",
        Name = "Votul creditorilor asupra planului",
        Description = "Creditorii votează planul de reorganizare în adunarea convocată în acest scop (Art. 138). " +
                      "Planul se consideră acceptat dacă obține votul creditorilor care dețin min. 30% din creanțele " +
                      "fiecărei clase și cel puțin o clasă acceptantă.",
        SortOrder = 110,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("ProcesVerbalVotPlan"),
        OutputTasksJson          = T(
          ("Convoaca adunarea creditorilor pentru votul planului (min. 14 zile)","Transmitere convocatoare cu planul și nota explicativă",14,"Email"),
          ("Organizează votul pe clase de creanțe (Art. 138)","Desfășurarea votului, constatarea întrunirii cvorumului",0,"Meeting"),
          ("Întocmește procesul-verbal al votului","PV cu rezultatele votului pe fiecare clasă de creditori",2,"Document"),
          ("Publică rezultatele votului în BPI","Publicare formală a rezultatelor",3,"Filing")
        ),
        AllowedTransitionsJson   = J("plan_confirmation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "plan_confirmation",
        Name = "Confirmare plan de reorganizare",
        Description = "Judecătorul sindic confirmă planul de reorganizare dacă au fost îndeplinite condițiile legale " +
                      "(Art. 139 Legea 85/2014). Confirmarea declanșează perioada de implementare (max. 3 ani).",
        SortOrder = 120,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("ProcesVerbalVotPlan"),
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Solicită confirmarea planului de reorganizare la tribunal","Depunere cerere confirmare cu PV vot și plan admis",7,"Filing"),
          ("Participă la ședința de confirmare a planului","Reprezintare la dosar, susținere plan",0,"Meeting"),
          ("Obtine sentința de confirmare a planului","Monitorizare pronunțare, ridicare sentință",14,"Filing"),
          ("Publică sentința de confirmare în BPI","Publicare formală conform art. 43",7,"Filing")
        ),
        AllowedTransitionsJson   = J("plan_implementation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "plan_implementation",
        Name = "Implementare plan de reorganizare",
        Description = "Debitorul execută măsurile prevăzute în planul confirmat (Art. 140 Legea 85/2014). " +
                      "Durata maximă: 3 ani (cu posibilitate de prelungire la 4 ani în cazuri excepționale). " +
                      "Practicianul supraveghează execuția și raportează trimestrial la creditori și tribunal.",
        SortOrder = 130,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Notifică debitorul și creditorii cu privire la confirmarea planului","Comunicare formală a sentinței de confirmare",3,"Email"),
          ("Instalează sistemul de monitorizare a implementării","Tablou de bord KPI conform indicatorilor din plan",7,"Review"),
          ("Redactează primul raport de implementare a planului (30 zile)","Raport privind stadiul primelor măsuri din plan",30,"Report")
        ),
        AllowedTransitionsJson   = J("plan_monitoring"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "plan_monitoring",
        Name = "Monitorizare implementare plan",
        Description = "Monitorizarea și raportarea periodică privind execuția planului de reorganizare (Art. 140-142). " +
                      "Dacă planul nu se execută, creditorii pot cere convertirea la faliment. " +
                      "La finalizare, administratorul solicită închiderea procedurii.",
        SortOrder = 140,
        ApplicableProcedureTypes = REORG,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("RaportImplementarePlan"),
        OutputTasksJson          = T(
          ("Redactează raportul trimestrial de implementare a planului","Raport privind progresul față de obiectivele planului",90,"Report"),
          ("Depune raportul trimestrial la tribunal și creditori","Distribuire raport și publicare BPI",90,"Filing"),
          ("Monitorizează indicatorii de performanță (KPI) din plan","Verificare îndeplinire jaloane și plăți programate",30,"Review")
        ),
        AllowedTransitionsJson   = J("final_report"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      // ════════════════════════════════════════════════════════════════════
      // BLOC 3: LICHIDARE ACTIVE  (Faliment / FalimentSimplificat – Art. 150-163)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "asset_inventory",
        Name = "Inventarierea averii debitorului",
        Description = "Lichidatorul judiciar inventariază toate bunurile mobile, imobile și drepturile ce constituie " +
                      "averea debitorului (Art. 150 Legea 85/2014). Sigilare bunuri, preluare registre.",
        SortOrder = 90,
        ApplicableProcedureTypes = FS_F,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("TabelDefinitiv"),
        OutputDocTypesJson       = J("RaportInventar"),
        OutputTasksJson          = T(
          ("Inventariază bunurile mobile ale debitorului","Listarea și codul de bare al bunurilor mobile",14,"Document"),
          ("Obține extrasele CF pentru toate imobilele debitorului","Identificare și inventariere bunuri imobile",7,"Document"),
          ("Inventariază creanțele și participațiile (acțiuni, părți sociale)","Listarea activelor financiare",14,"Document"),
          ("Redactează raportul de inventariere (Art. 150)","Raportul complet al averii debitorului",21,"Document"),
          ("Sigilează bunurile susceptibile de deteriorare sau sustracție","Aplicare sigilii și predare în custodie",7,"Compliance")
        ),
        AllowedTransitionsJson   = J("asset_valuation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "asset_valuation",
        Name = "Evaluarea bunurilor",
        Description = "Numirea unui evaluator autorizat ANEVAR pentru evaluarea bunurilor din masa falimentului " +
                      "(Art. 154 Legea 85/2014). Aprobarea metodologiei de vânzare de către comitetul creditorilor.",
        SortOrder = 100,
        ApplicableProcedureTypes = FS_F,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("RaportInventar"),
        OutputDocTypesJson       = J("RaportEvaluare"),
        OutputTasksJson          = T(
          ("Numește evaluatorul de bunuri (cu avizul comitetului creditorilor)","Selectare evaluator autorizat ANEVAR",14,"Meeting"),
          ("Obține raportul de evaluare pentru bunuri imobile","Evaluare la valoare de piață și valoare de lichidare",30,"Document"),
          ("Obține raportul de evaluare pentru bunuri mobile și echipamente","Evaluare echipamente, stocuri, vehicule",21,"Document"),
          ("Aprobă metodologia de vânzare (licitație / negociere directă)","Decizie comitet creditori privind modul de valorificare",14,"Meeting")
        ),
        AllowedTransitionsJson   = J("asset_liquidation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "asset_liquidation",
        Name = "Lichidarea activelor",
        Description = "Valorificarea bunurilor din averea debitorului prin licitații publice sau negociere directă " +
                      "(Art. 154-157 Legea 85/2014). Publicitate obligatorie min. 15 zile înainte. " +
                      "Sumele obținute se depun în contul special al procedurii.",
        SortOrder = 110,
        ApplicableProcedureTypes = FS_F,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("RaportEvaluare"),
        OutputDocTypesJson       = J("RaportLichidare","AnuntLicitatie"),
        OutputTasksJson          = T(
          ("Publică anunțul de vânzare în ziare și AAAS (min. 15 zile)","Publicitate obligatorie conform art. 154",15,"Filing"),
          ("Organizează licitațiile publice pentru bunurile debitorului","Proceduri licitație deschisă cu strigare",0,"Meeting"),
          ("Redactează procesele-verbale de adjudecare","PV pentru fiecare bun adjudecat",3,"Document"),
          ("Încasează prețul adjudecat și depune în contul de lichidare","Gestionarea sumelor obținute din vânzare",7,"Payment"),
          ("Redactează raportul periodic de lichidare a activelor","Situația vânzărilor, sume obținute, bunuri nevândute",30,"Report")
        ),
        AllowedTransitionsJson   = J("distribution"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "distribution",
        Name = "Distribuirea sumelor către creditori",
        Description = "Întocmirea planului de distribuire a sumelor obținute din lichidare (Art. 161-163 Legea 85/2014). " +
                      "Distribuirea se face în ordinea de prioritate a creanțelor (Art. 159). " +
                      "Planul de distribuire se publică în BPI și se aprobă de judecătorul sindic.",
        SortOrder = 120,
        ApplicableProcedureTypes = FS_F,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("RaportLichidare"),
        OutputDocTypesJson       = J("PlanDistributie"),
        OutputTasksJson          = T(
          ("Întocmește planul de distribuire a fondurilor (Art. 161)","Conform ordinii de preferință Art. 159, categorii 1-8",14,"Document"),
          ("Publică planul de distribuire în BPI","Publicare formală cu termen de contestatie 5 zile",14,"Filing"),
          ("Obține aprobarea judecătorului sindic pentru distribuire","Participare termen aprobare plan distribuire",14,"Filing"),
          ("Efectuează plățile conform planului de distribuire aprobat","Viramente bancare creditori conform plan aprobat",7,"Payment"),
          ("Redactează procesul-verbal de distribuire","PV cu sumele efectiv distribuite",3,"Document")
        ),
        AllowedTransitionsJson   = J("final_report"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      // ════════════════════════════════════════════════════════════════════
      // ETAPĂ FINALĂ  (Toate procedurile principale)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "final_report",
        Name = "Raport final și închidere procedură",
        Description = "Administratorul/lichidatorul judiciar întocmește raportul final (Art. 167 Legea 85/2014). " +
                      "Judecătorul sindic pronunță sentința de închidere a procedurii. " +
                      "ONRC și ANAF sunt notificate; debitorul persoană juridică se radiază din RC.",
        SortOrder = 200,
        ApplicableProcedureTypes = ALL4,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("RaportFinal"),
        OutputTasksJson          = T(
          ("Redactează raportul final (Art. 167)","Descrierea activității, distribuirilor efectuate, situația finală",14,"Report"),
          ("Depune raportul final la tribunal și publică în BPI","Depunere dosar + publicare BPI raport final",14,"Filing"),
          ("Solicită pronunțarea sentinței de închidere a procedurii","Cerere confirmare că toate etapele sunt finalizate",7,"Filing"),
          ("Notifică ONRC / ANAF cu privire la închiderea procedurii","Transmitere sentință de închidere la instituții",7,"Filing"),
          ("Radierea debitorului din Registrul Comerțului (post-în chidere)","Depunere dosar radiere la ONRC",30,"Compliance")
        ),
        AllowedTransitionsJson   = "[]",
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      // ════════════════════════════════════════════════════════════════════
      // BLOC 4: CONCORDAT PREVENTIV  (Art. 31-50 Legea 85/2014)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "concordat_request",
        Name = "Cerere deschidere concordat preventiv",
        Description = "Debitorul aflat în dificultate financiară, dar neinsolvabil, depune cerere la tribunal " +
                      "pentru deschiderea procedurii de concordat preventiv (Art. 17 Legea 85/2014). " +
                      "Se anexează oferta de concordat și lista creditorii.",
        SortOrder = 10,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = J("CaseNumber","DebtorName","CourtName"),
        RequiredPartyRolesJson   = J("Debtor"),
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("CerereConcordat"),
        OutputTasksJson          = T(
          ("Redactează cererea de deschidere a concordatuli preventiv","Includerea ofertei de concordat și listei creditorii (Art. 17)",7,"Document"),
          ("Întocmește lista creditorii cu valorile creanțelor","Situație completă a tuturor creditorii",7,"Document"),
          ("Depune cererea la tribunalul competent","Depunere dosar cerere deschidere concordat",7,"Filing")
        ),
        AllowedTransitionsJson   = J("concordat_negotiation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_negotiation",
        Name = "Notificarea creditorii și negocieri",
        Description = "Conciliatorul notifică toți creditorii și inițiază negocierile cu aceștia (Art. 21-23). " +
                      "Scopul este obținerea acordului unui număr suficient de creditori pentru finalizarea concordatuli.",
        SortOrder = 30,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("CerereConcordat"),
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Notifică toți creditorii cu privire la deschiderea concordatuli","Transmitere notificări formale creditorii",14,"Email"),
          ("Inițiază negocierile cu creditorii principali (>10% creanțe)","Ședințe de negociere individuală sau colectivă",30,"Meeting"),
          ("Obține acorduri preliminare de la creditori (min. 75% din creanțe)","Consemnarea punctelor de acord în scris",45,"Document")
        ),
        AllowedTransitionsJson   = J("concordat_drafting"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_drafting",
        Name = "Elaborare act de concordat",
        Description = "Conciliatorul elaborează actul de concordat preventiv (Art. 25 Legea 85/2014), " +
                      "conținând proiecția financiară, reducerile/eșalonările de creanțe, garanțiile oferite " +
                      "și calendarul de executare (maxim 24 de luni).",
        SortOrder = 40,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("ActConcordat"),
        OutputTasksJson          = T(
          ("Redactează actul de concordat preventiv (Art. 25)","Includerea planului financiar, reducerilor creanțe, garanțiilor",14,"Document"),
          ("Consultații finale cu creditorii privind termenii actuli","Ajustarea clauzelor conform negocierilor",10,"Meeting")
        ),
        AllowedTransitionsJson   = J("concordat_vote"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_vote",
        Name = "Votul creditorii",
        Description = "Creditorii votează actul de concordat (Art. 26 Legea 85/2014). " +
                      "Concordatul se consideră adoptat cu votul titularilor a min. 75% din totalul creanțelor.",
        SortOrder = 50,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("ActConcordat"),
        OutputDocTypesJson       = J("ProcesVerbalVotConcordat"),
        OutputTasksJson          = T(
          ("Organizează ședința de vot a creditorii","Convocare formală min. 5 zile înainte",5,"Meeting"),
          ("Colectează voturile creditorii (inclusiv prin corespondență)","Centralizarea voturilor, calculul procentului din creanțe",7,"Document"),
          ("Redactează procesul-verbal al votului","PV cu rezultatul votului pe fiecare creditor",2,"Document")
        ),
        AllowedTransitionsJson   = J("concordat_homologation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_homologation",
        Name = "Omologarea concordatuli de instanță",
        Description = "Tribunalul omologhează concordatul preventiv dacă sunt îndeplinite condițiile legale " +
                      "(Art. 28 Legea 85/2014). Sentința de omologare produce efecte față de toți creditorii " +
                      "(inclusiv cei care nu au votat sau au votat împotrivă).",
        SortOrder = 60,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = J("ProcesVerbalVotConcordat"),
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Depune actul de concordat la tribunal pentru omologare","Dosar cu actul concordat + PV vot + documente suport",7,"Filing"),
          ("Obține sentința de omologare a concordatuli (Art. 28)","Urmărire pronunțare, ridicare sentință",21,"Filing"),
          ("Publică hotărârea de omologare în BPI","Publicare formală conform obligații BPI",7,"Filing")
        ),
        AllowedTransitionsJson   = J("concordat_implementation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_implementation",
        Name = "Executarea concordatuli preventiv",
        Description = "Debitorul execută măsurile din concordatul omologat (Art. 31-35 Legea 85/2014). " +
                      "Conciliatorul monitorizează executarea și raportează trimestrial. " +
                      "Durata maximă de executare: 24 de luni de la omologare.",
        SortOrder = 70,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("RaportExecutareConcordat"),
        OutputTasksJson          = T(
          ("Monitorizează executarea concordatuli (trimestrial)","Verificarea respectării planului de plăți și a măsurilor",90,"Review"),
          ("Redactează raportul trimestrial de executare","Situația plăților efectuate, gradul de îndeplinire a planului",90,"Report"),
          ("Prezintă raportul în fața judecătorului sindic","Depunere și comunicare creditorii",90,"Filing")
        ),
        AllowedTransitionsJson   = J("concordat_completion"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "concordat_completion",
        Name = "Finalizare / Reziliere concordat preventiv",
        Description = "La îndeplinirea obligațiilor din concordat, conciliatorul solicită tribunalului " +
                      "constatarea finalizării procedurii (Art. 36 Legea 85/2014). " +
                      "În caz de neîndeplinire, creditorii pot cere rezilierea și deschiderea insolvenței.",
        SortOrder = 80,
        ApplicableProcedureTypes = CONC,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Redactează raportul final de executare a concordatuli","Situație finală: obligații îndeplinite, plăți efectuate",14,"Report"),
          ("Solicită tribunalului constatarea finalizării concordatuli","Cerere și dosar probatoriu (Art. 36)",7,"Filing"),
          ("Notifică creditorii cu privire la finalizarea procedurii","Comunicare formală a sentinței finale",7,"Email")
        ),
        AllowedTransitionsJson   = "[]",
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      // ════════════════════════════════════════════════════════════════════
      // BLOC 5: MANDAT AD-HOC  (Art. 21-30 Legea 85/2014)
      // ════════════════════════════════════════════════════════════════════

      new()
      {
        TenantId = null,
        StageKey = "mandate_request",
        Name = "Cerere numire mandatar ad-hoc",
        Description = "Debitorul depune la tribunal cererea de numire a unui mandatar ad-hoc (Art. 7 Legea 85/2014). " +
                      "Procedura este confidențială; mandatarul negociază cu creditorii pentru evitarea insolvenței.",
        SortOrder = 10,
        ApplicableProcedureTypes = MAND,
        RequiredFieldsJson       = J("CaseNumber","DebtorName","CourtName"),
        RequiredPartyRolesJson   = J("Debtor"),
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Redactează și depune cererea de numire mandatar ad-hoc (Art. 7)","Cerere motivată cu situația financiară a debitorului",7,"Filing"),
          ("Propune un mandatar ad-hoc dintre practicienii UNPIR","Identificare candidat și obținere confirmare disponibilitate",5,"Document")
        ),
        AllowedTransitionsJson   = J("mandate_appointment"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "mandate_appointment",
        Name = "Numire mandatar ad-hoc",
        Description = "Judecătorul sindic numește mandatarul ad-hoc prin sentință (Art. 8 Legea 85/2014). " +
                      "Misiunea mandatarului este de a negocia cu creditorii și de a identifica soluții de redresare.",
        SortOrder = 20,
        ApplicableProcedureTypes = MAND,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Obține sentința de numire a mandatarului ad-hoc","Ridicare sentință de la tribunal",14,"Filing"),
          ("Acceptă mandatul și transmite confirmarea debitorului","Comunicare formală a acceptării misiunii",3,"Email"),
          ("Analizează situația financiară a debitorului","Evaluarea datoriilor, activelor și flux de numerar",14,"Review")
        ),
        AllowedTransitionsJson   = J("mandate_negotiation"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "mandate_negotiation",
        Name = "Negocieri cu creditorii",
        Description = "Mandatarul ad-hoc conduce negocieri confidențiale cu creditorii principali (Art. 10-12). " +
                      "Sunt propuse soluții: eșalonări, reduceri de creanțe, conversii, majorări de capital. " +
                      "Procedura este confidențiala și nu se publică în BPI.",
        SortOrder = 30,
        ApplicableProcedureTypes = MAND,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Identifică creditorii principali și valorile creanțelor","Cartografierea datoriilor și a creditorii cheie",7,"Document"),
          ("Inițiază negocierile confidențiale (Art. 10)","Ședințe individuale cu fiecare creditor major",30,"Meeting"),
          ("Propune soluții de redresare financiară creditorii","Eșalonări, renegocieri, conversii datorie–capital",30,"Meeting")
        ),
        AllowedTransitionsJson   = J("mandate_agreement"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "mandate_agreement",
        Name = "Acord negociat cu creditorii",
        Description = "Mandatarul finalizează și autentifică acordul cu creditorii (Art. 13 Legea 85/2014). " +
                      "Acordul nu necesită omologare judiciară dacă creditorii sunt de acord. " +
                      "Produce efecte numai față de semnatari.",
        SortOrder = 40,
        ApplicableProcedureTypes = MAND,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = J("AcordAdHoc"),
        OutputTasksJson          = T(
          ("Redactează acordul negociat (Art. 13)","Document cu termenii agreați de toate părțile",14,"Document"),
          ("Obține semnăturile tuturor părților la acord","Semnare debitor și toți creditorii semnatari",7,"Document"),
          ("Autentifică acordul la notar (dacă se solicită)","Autentificare notarială pentru opozabilitate",7,"Compliance")
        ),
        AllowedTransitionsJson   = J("mandate_termination"),
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },

      new()
      {
        TenantId = null,
        StageKey = "mandate_termination",
        Name = "Încetare mandat ad-hoc",
        Description = "Mandatul ad-hoc încetează prin: îndeplinirea misiunii (acord semnat), " +
                      "neînțelegerea cu debitorul, trecerea termenului (Art. 14 Legea 85/2014), " +
                      "sau deschiderea insolvenței. Mandatarul depune raportul final.",
        SortOrder = 50,
        ApplicableProcedureTypes = MAND,
        RequiredFieldsJson       = "[]",
        RequiredPartyRolesJson   = "[]",
        RequiredDocTypesJson     = "[]",
        OutputDocTypesJson       = "[]",
        OutputTasksJson          = T(
          ("Redactează raportul final al mandatarului ad-hoc","Situație finală: negocieri, acorduri, rezultate obținute",7,"Report"),
          ("Notifică tribunalul cu privire la finalizarea mandatuli","Comunicare formală a încetării misiunii",3,"Filing"),
          ("Restituie documentele debitorului","Predare dosar complet debitorului",3,"Compliance")
        ),
        AllowedTransitionsJson   = "[]",
        IsActive = true,
        CreatedOn = now, CreatedBy = "System",
      },
    };

    var toAdd = stages.Where(s => !existingKeys.Contains(s.StageKey)).ToList();
    if (toAdd.Count == 0) return;

    db.WorkflowStageDefinitions.AddRange(toAdd);
    await db.SaveChangesAsync();
  }
}
