using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;

namespace Insolvex.Data;

public class ApplicationDbContext : DbContext
{
  private readonly ICurrentUserService? _currentUser;

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUser = null)
      : base(options)
  {
    _currentUser = currentUser;
  }

  // Entities
  public DbSet<Tenant> Tenants => Set<Tenant>();
  public DbSet<User> Users => Set<User>();
  public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
  public DbSet<Company> Companies => Set<Company>();
  public DbSet<InsolvencyCase> InsolvencyCases => Set<InsolvencyCase>();
  public DbSet<InsolvencyDocument> InsolvencyDocuments => Set<InsolvencyDocument>();
  public DbSet<CompanyTask> CompanyTasks => Set<CompanyTask>();
  public DbSet<CaseParty> CaseParties => Set<CaseParty>();
  public DbSet<InsolvencyFirm> InsolvencyFirms => Set<InsolvencyFirm>();
  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
  public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
  public DbSet<ScheduledEmail> ScheduledEmails => Set<ScheduledEmail>();
  public DbSet<PendingUpload> PendingUploads => Set<PendingUpload>();
  public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
  public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
  public DbSet<UserSigningKey> UserSigningKeys => Set<UserSigningKey>();
  public DbSet<DigitalSignature> DigitalSignatures => Set<DigitalSignature>();
  public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
  public DbSet<CaseSummary> CaseSummaries => Set<CaseSummary>();
  public DbSet<Tribunal> Tribunals => Set<Tribunal>();
  public DbSet<FinanceAuthority> FinanceAuthorities => Set<FinanceAuthority>();
  public DbSet<LocalGovernment> LocalGovernments => Set<LocalGovernment>();
  public DbSet<GeneratedLetter> GeneratedLetters => Set<GeneratedLetter>();
  public DbSet<TenantDeadlineSettings> TenantDeadlineSettings => Set<TenantDeadlineSettings>();
  public DbSet<CaseDeadlineOverride> CaseDeadlineOverrides => Set<CaseDeadlineOverride>();
  public DbSet<ONRCFirmRecord> ONRCFirmRecords => Set<ONRCFirmRecord>();
  public DbSet<CaseEvent> CaseEvents => Set<CaseEvent>();
  public DbSet<AiSystemConfig> AiSystemConfigs => Set<AiSystemConfig>();
  public DbSet<WorkflowStageDefinition> WorkflowStageDefinitions => Set<WorkflowStageDefinition>();
  public DbSet<WorkflowStageTemplate> WorkflowStageTemplates => Set<WorkflowStageTemplate>();
  public DbSet<CreditorClaim> CreditorClaims => Set<CreditorClaim>();
  public DbSet<Asset> Assets => Set<Asset>();
  public DbSet<CaseWorkflowStage> CaseWorkflowStages => Set<CaseWorkflowStage>();
  public DbSet<CaseDeadline> CaseDeadlines => Set<CaseDeadline>();
  public DbSet<TaskNote> TaskNotes => Set<TaskNote>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // ----- Entity configurations -----

    // Tenant
    modelBuilder.Entity<Tenant>(e =>
    {
      e.HasKey(t => t.Id);
      e.Property(t => t.Name).HasMaxLength(256).IsRequired();
      e.Property(t => t.Domain).HasMaxLength(256);
      e.Property(t => t.PlanName).HasMaxLength(128);
      e.HasIndex(t => t.Domain).IsUnique().HasFilter("[Domain] IS NOT NULL");
    });

    // User
    modelBuilder.Entity<User>(e =>
{
  e.HasKey(u => u.Id);
  e.Property(u => u.Email).HasMaxLength(256).IsRequired();
  e.Property(u => u.FirstName).HasMaxLength(128).IsRequired();
  e.Property(u => u.LastName).HasMaxLength(128).IsRequired();
  e.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
  e.Property(u => u.ResetToken).HasMaxLength(256);
  e.Property(u => u.AvatarUrl).HasMaxLength(512);
  e.Property(u => u.UseSavedSigningKey).HasDefaultValue(true);
  e.HasIndex(u => u.Email).IsUnique();
  e.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
});

    // UserInvitation
    modelBuilder.Entity<UserInvitation>(e =>
    {
      e.HasKey(i => i.Id);
      e.Property(i => i.Email).HasMaxLength(256).IsRequired();
      e.Property(i => i.FirstName).HasMaxLength(128);
      e.Property(i => i.LastName).HasMaxLength(128);
      e.Property(i => i.Token).HasMaxLength(256).IsRequired();
      e.HasIndex(i => i.Token).IsUnique();
      e.HasOne(i => i.InvitedBy).WithMany(u => u.SentInvitations).HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.SetNull);
    });

    // Company
    modelBuilder.Entity<Company>(e =>
    {
      e.HasKey(c => c.Id);
      e.Property(c => c.Name).HasMaxLength(512).IsRequired();
      e.Property(c => c.CuiRo).HasMaxLength(64);
      e.Property(c => c.TradeRegisterNo).HasMaxLength(128);
      e.Property(c => c.VatNumber).HasMaxLength(64);
      e.Property(c => c.Address).HasMaxLength(512);
      e.Property(c => c.Locality).HasMaxLength(256);
      e.Property(c => c.County).HasMaxLength(256);
      e.Property(c => c.Country).HasMaxLength(128);
      e.Property(c => c.PostalCode).HasMaxLength(16);
      e.Property(c => c.Caen).HasMaxLength(32);
      e.Property(c => c.IncorporationYear).HasMaxLength(10);
      e.Property(c => c.ShareCapitalRon).HasColumnType("decimal(18,2)");
      e.Property(c => c.Phone).HasMaxLength(64);
      e.Property(c => c.Email).HasMaxLength(256);
      e.Property(c => c.ContactPerson).HasMaxLength(256);
      e.Property(c => c.Iban).HasMaxLength(64);
      e.Property(c => c.BankName).HasMaxLength(256);

      e.HasOne(c => c.AssignedTo).WithMany().HasForeignKey(c => c.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
      e.HasOne(c => c.Tenant).WithMany(t => t.Companies).HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
    });

    // InsolvencyCase
    modelBuilder.Entity<InsolvencyCase>(e =>
         {
           e.HasKey(c => c.Id);
           e.Property(c => c.CaseNumber).HasMaxLength(128).IsRequired();
           e.Property(c => c.CourtName).HasMaxLength(512);
           e.Property(c => c.CourtSection).HasMaxLength(256);
           e.Property(c => c.JudgeSyndic).HasMaxLength(256);
           e.Property(c => c.DebtorName).HasMaxLength(512).IsRequired();
           e.Property(c => c.DebtorCui).HasMaxLength(64);
           e.Property(c => c.LawReference).HasMaxLength(256);
           e.Property(c => c.PractitionerName).HasMaxLength(256);
           e.Property(c => c.PractitionerRole).HasMaxLength(128);
           e.Property(c => c.PractitionerFiscalId).HasMaxLength(64);
           e.Property(c => c.PractitionerDecisionNo).HasMaxLength(128);
           e.Property(c => c.TotalClaimsRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.SecuredClaimsRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.UnsecuredClaimsRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.BudgetaryClaimsRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.EmployeeClaimsRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.EstimatedAssetValueRon).HasColumnType("decimal(18,2)");
           e.Property(c => c.BpiPublicationNo).HasMaxLength(128);
           e.Property(c => c.OpeningDecisionNo).HasMaxLength(128);
           e.Property(c => c.Notes).HasMaxLength(4000);
           e.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.SetNull);
           e.HasOne(c => c.AssignedTo).WithMany(u => u.AssignedCases).HasForeignKey(c => c.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
           e.HasOne(c => c.Tenant).WithMany(t => t.Cases).HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
         });

    // CaseParty
    modelBuilder.Entity<CaseParty>(e =>
           {
             e.HasKey(p => p.Id);
             e.Property(p => p.RoleDescription).HasMaxLength(256);
             e.Property(p => p.ClaimAmountRon).HasColumnType("decimal(18,2)");
             e.Property(p => p.Notes).HasMaxLength(2000);
             e.HasOne(p => p.Case).WithMany(c => c.Parties).HasForeignKey(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
             e.HasOne(p => p.Company).WithMany(c => c.CaseParties).HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
             e.HasIndex(p => new { p.CaseId, p.CompanyId, p.Role }).IsUnique();
           });

    // CompanyTask
    modelBuilder.Entity<CompanyTask>(e =>
          {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(512).IsRequired();
            e.Property(t => t.Description).HasMaxLength(4000);
            e.Property(t => t.Labels).HasMaxLength(1024);
            e.Property(t => t.DeadlineSource).HasMaxLength(64);
            e.Property(t => t.Category).HasMaxLength(64);
            e.Property(t => t.EscalationPolicyId).HasMaxLength(128);
            e.Property(t => t.ReminderScheduleId).HasMaxLength(128);
            e.HasOne(t => t.Company).WithMany(c => c.Tasks).HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Case).WithMany(ic => ic.Tasks).HasForeignKey(t => t.CaseId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.AssignedTo).WithMany(u => u.AssignedTasks).HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(t => new { t.CaseId, t.Status });
            e.HasIndex(t => new { t.AssignedToUserId, t.Status });
            e.HasIndex(t => t.Deadline);
          });

    // TaskNote
    modelBuilder.Entity<TaskNote>(e =>
          {
            e.HasKey(n => n.Id);
            e.Property(n => n.Content).HasMaxLength(4000).IsRequired();
            e.Property(n => n.CreatedByName).HasMaxLength(256).IsRequired();
            e.HasOne(n => n.Task).WithMany(t => t.Notes).HasForeignKey(n => n.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => n.TaskId);
          });

    // InsolvencyFirm
    modelBuilder.Entity<InsolvencyFirm>(e =>
  {
    e.HasKey(f => f.Id);
    e.Property(f => f.FirmName).HasMaxLength(512).IsRequired();
    e.Property(f => f.CuiRo).HasMaxLength(64);
    e.Property(f => f.TradeRegisterNo).HasMaxLength(128);
    e.Property(f => f.VatNumber).HasMaxLength(64);
    e.Property(f => f.UnpirRegistrationNo).HasMaxLength(128);
    e.Property(f => f.UnpirRfo).HasMaxLength(64);
    e.Property(f => f.Address).HasMaxLength(512);
    e.Property(f => f.Locality).HasMaxLength(256);
    e.Property(f => f.County).HasMaxLength(256);
    e.Property(f => f.Country).HasMaxLength(128);
    e.Property(f => f.PostalCode).HasMaxLength(16);
    e.Property(f => f.Phone).HasMaxLength(64);
    e.Property(f => f.Fax).HasMaxLength(64);
    e.Property(f => f.Email).HasMaxLength(256);
    e.Property(f => f.Website).HasMaxLength(256);
    e.Property(f => f.ContactPerson).HasMaxLength(256);
    e.Property(f => f.Iban).HasMaxLength(64);
    e.Property(f => f.BankName).HasMaxLength(256);
    e.Property(f => f.SecondaryIban).HasMaxLength(64);
    e.Property(f => f.SecondaryBankName).HasMaxLength(256);
    e.Property(f => f.LogoUrl).HasMaxLength(512);
    e.HasOne(f => f.Tenant).WithOne(t => t.InsolvencyFirm).HasForeignKey<InsolvencyFirm>(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(f => f.TenantId).IsUnique();
  });

    // PendingUpload
    modelBuilder.Entity<PendingUpload>(e =>
 {
   e.HasKey(p => p.Id);
   e.Property(p => p.OriginalFileName).HasMaxLength(512).IsRequired();
   e.Property(p => p.StoredFileName).HasMaxLength(512).IsRequired();
   e.Property(p => p.FilePath).HasMaxLength(1024).IsRequired();
   e.Property(p => p.ContentType).HasMaxLength(128);
   e.Property(p => p.UploadedByEmail).HasMaxLength(256);
   e.Property(p => p.RecommendedAction).HasMaxLength(64);
   e.Property(p => p.DetectedDocType).HasMaxLength(256);
   e.Property(p => p.DetectedCaseNumber).HasMaxLength(128);
   e.Property(p => p.DetectedDebtorName).HasMaxLength(512);
   e.Property(p => p.DetectedCourtName).HasMaxLength(512);
   e.Property(p => p.DetectedCourtSection).HasMaxLength(256);
   e.Property(p => p.DetectedJudgeSyndic).HasMaxLength(256);
   e.HasIndex(p => p.TenantId);
 });

    // ScheduledEmail
    modelBuilder.Entity<ScheduledEmail>(e =>
          {
            e.HasKey(s => s.Id);
            e.Property(s => s.To).HasMaxLength(512).IsRequired();
            e.Property(s => s.Cc).HasMaxLength(512);
            e.Property(s => s.Bcc).HasMaxLength(512);
            e.Property(s => s.Subject).HasMaxLength(512).IsRequired();
            e.Property(s => s.Status).HasMaxLength(32);
            e.Property(s => s.ProviderMessageId).HasMaxLength(256);
            e.HasOne(s => s.Case).WithMany(ic => ic.Emails).HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(s => new { s.IsSent, s.ScheduledFor });
            e.HasIndex(s => s.CaseId);
          });

    // SystemConfig
    modelBuilder.Entity<SystemConfig>(e =>
{
e.HasKey(c => c.Id);
e.Property(c => c.Key).HasMaxLength(256).IsRequired();
e.Property(c => c.Value).HasMaxLength(4000).IsRequired();
e.Property(c => c.Description).HasMaxLength(1000);
e.Property(c => c.Group).HasMaxLength(128);
e.HasIndex(c => c.Key).IsUnique();
});

    // DocumentTemplate
    modelBuilder.Entity<DocumentTemplate>(e =>
    {
      e.HasKey(t => t.Id);
      e.Property(t => t.Name).HasMaxLength(256).IsRequired();
      e.Property(t => t.FileName).HasMaxLength(512).IsRequired();
      e.Property(t => t.StorageKey).HasMaxLength(1024).IsRequired();
      e.Property(t => t.ContentType).HasMaxLength(128).IsRequired();
      e.Property(t => t.FileHash).HasMaxLength(128);
      e.Property(t => t.Description).HasMaxLength(2000);
      e.Property(t => t.Stage).HasMaxLength(128);
      e.Property(t => t.MergeFieldsJson).HasMaxLength(4000);
      e.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
      // Unique: one template type per tenant (null = global)
      e.HasIndex(t => new { t.TenantId, t.TemplateType }).IsUnique();
    });


    // UserSigningKey
    modelBuilder.Entity<UserSigningKey>(e =>
    {
      e.HasKey(k => k.Id);
      e.Property(k => k.Name).HasMaxLength(256).IsRequired();
      e.Property(k => k.SubjectName).HasMaxLength(512);
      e.Property(k => k.IssuerName).HasMaxLength(512);
      e.Property(k => k.SerialNumber).HasMaxLength(128);
      e.Property(k => k.Thumbprint).HasMaxLength(128);
      e.Property(k => k.EncryptedPfxData).IsRequired();
      e.Property(k => k.EncryptionNonce).IsRequired();
      e.Property(k => k.EncryptionTag).IsRequired();
      e.HasOne(k => k.User).WithMany(u => u.SigningKeys).HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
      e.HasIndex(k => new { k.UserId, k.IsActive });
    });

    // DigitalSignature
    modelBuilder.Entity<DigitalSignature>(e =>
{
e.HasKey(s => s.Id);
e.Property(s => s.DocumentHash).HasMaxLength(128).IsRequired();
e.Property(s => s.SignatureData).IsRequired();
e.Property(s => s.CertificateSubject).HasMaxLength(512);
e.Property(s => s.CertificateThumbprint).HasMaxLength(128);
e.Property(s => s.CertificateSerialNumber).HasMaxLength(128);
e.Property(s => s.Reason).HasMaxLength(1000);
e.HasOne(s => s.Document).WithMany(d => d.Signatures).HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
e.HasOne(s => s.SignedBy).WithMany(u => u.Signatures).HasForeignKey(s => s.SignedByUserId).OnDelete(DeleteBehavior.Restrict);
e.HasOne(s => s.SigningKey).WithMany().HasForeignKey(s => s.SigningKeyId).OnDelete(DeleteBehavior.SetNull);
e.HasIndex(s => s.DocumentId);
});

    // CalendarEvent
    modelBuilder.Entity<CalendarEvent>(e =>
    {
      e.HasKey(c => c.Id);
      e.Property(c => c.Title).HasMaxLength(512).IsRequired();
      e.Property(c => c.Description).HasMaxLength(4000);
      e.Property(c => c.Location).HasMaxLength(1024);
      e.Property(c => c.EventType).HasMaxLength(64).IsRequired();
      e.Property(c => c.ParticipantsJson).HasMaxLength(4000);
      e.Property(c => c.IcsUrl).HasMaxLength(1024);
      e.HasOne(c => c.Case).WithMany(ic => ic.CalendarEvents).HasForeignKey(c => c.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasIndex(c => new { c.CaseId, c.Start });
    });

    // CaseSummary
    modelBuilder.Entity<CaseSummary>(e =>
{
e.HasKey(s => s.Id);
e.Property(s => s.Model).HasMaxLength(128);
e.Property(s => s.Trigger).HasMaxLength(256);
e.HasOne(s => s.Case).WithMany(ic => ic.Summaries).HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.Cascade);
e.HasIndex(s => new { s.CaseId, s.GeneratedAt });
});

    // Tribunal
    modelBuilder.Entity<Tribunal>(e =>
    {
      e.HasKey(t => t.Id);
      e.Property(t => t.Name).HasMaxLength(512).IsRequired();
      e.Property(t => t.Section).HasMaxLength(256);
      e.Property(t => t.Locality).HasMaxLength(256);
      e.Property(t => t.County).HasMaxLength(256);
      e.Property(t => t.Address).HasMaxLength(512);
      e.Property(t => t.PostalCode).HasMaxLength(16);
      e.Property(t => t.RegistryPhone).HasMaxLength(64);
      e.Property(t => t.RegistryFax).HasMaxLength(64);
      e.Property(t => t.RegistryEmail).HasMaxLength(256);
      e.Property(t => t.RegistryHours).HasMaxLength(256);
      e.Property(t => t.Website).HasMaxLength(256);
      e.Property(t => t.ContactPerson).HasMaxLength(256);
      e.Property(t => t.Notes).HasMaxLength(2000);
      e.HasIndex(t => new { t.TenantId, t.Name });
    });

    // FinanceAuthority (ANAF)
    modelBuilder.Entity<FinanceAuthority>(e =>
        {
          e.HasKey(t => t.Id);
          e.Property(t => t.Name).HasMaxLength(512).IsRequired();
          e.Property(t => t.Locality).HasMaxLength(256);
          e.Property(t => t.County).HasMaxLength(256);
          e.Property(t => t.Address).HasMaxLength(512);
          e.Property(t => t.PostalCode).HasMaxLength(16);
          e.Property(t => t.Phone).HasMaxLength(64);
          e.Property(t => t.Fax).HasMaxLength(64);
          e.Property(t => t.Email).HasMaxLength(256);
          e.Property(t => t.Website).HasMaxLength(256);
          e.Property(t => t.ContactPerson).HasMaxLength(256);
          e.Property(t => t.ScheduleHours).HasMaxLength(256);
          e.Property(t => t.Notes).HasMaxLength(2000);
          e.HasIndex(t => new { t.TenantId, t.Name });
          e.HasOne(t => t.Parent)
              .WithMany(t => t.Children)
              .HasForeignKey(t => t.ParentId)
              .OnDelete(DeleteBehavior.Restrict);
        });

    // LocalGovernment
    modelBuilder.Entity<LocalGovernment>(e =>
{
e.HasKey(t => t.Id);
e.Property(t => t.Name).HasMaxLength(512).IsRequired();
e.Property(t => t.Locality).HasMaxLength(256);
e.Property(t => t.County).HasMaxLength(256);
e.Property(t => t.Address).HasMaxLength(512);
e.Property(t => t.PostalCode).HasMaxLength(16);
e.Property(t => t.Phone).HasMaxLength(64);
e.Property(t => t.Fax).HasMaxLength(64);
e.Property(t => t.Email).HasMaxLength(256);
e.Property(t => t.Website).HasMaxLength(256);
e.Property(t => t.ContactPerson).HasMaxLength(256);
e.Property(t => t.ScheduleHours).HasMaxLength(256);
e.Property(t => t.Notes).HasMaxLength(2000);
e.HasIndex(t => new { t.TenantId, t.Name });
});

    // GeneratedLetter
    modelBuilder.Entity<GeneratedLetter>(e =>
    {
      e.HasKey(g => g.Id);
      e.Property(g => g.StorageKey).HasMaxLength(1024).IsRequired();
      e.Property(g => g.FileName).HasMaxLength(512).IsRequired();
      e.Property(g => g.ContentType).HasMaxLength(128);
      e.Property(g => g.FileHash).HasMaxLength(128);
      e.Property(g => g.DeliveryStatus).HasMaxLength(32);
      e.Property(g => g.ErrorMessage).HasMaxLength(2000);
      e.HasOne(g => g.Case).WithMany(ic => ic.GeneratedLetters)
             .HasForeignKey(g => g.CaseId).OnDelete(DeleteBehavior.NoAction);
      e.HasOne(g => g.Template).WithMany()
        .HasForeignKey(g => g.TemplateId).OnDelete(DeleteBehavior.SetNull);
      e.HasIndex(g => new { g.CaseId, g.TemplateType });
    });

    // Override the TenantId FK delete behaviour for GeneratedLetter after query filters are applied.
    // Must be done AFTER ApplyTenantQueryFilters() since that loop registers the FK.
    // We do this in a model-finalizing step below.

    // TenantDeadlineSettings
    modelBuilder.Entity<TenantDeadlineSettings>(e =>
{
  e.HasKey(t => t.Id);
  e.Property(t => t.ReminderDaysBeforeDeadline).HasMaxLength(128);
  e.Property(t => t.EmailSendingDomain).HasMaxLength(256);
  e.Property(t => t.EmailFromName).HasMaxLength(256);
  e.HasIndex(t => t.TenantId).IsUnique();
});

    // CaseDeadlineOverride
    modelBuilder.Entity<CaseDeadlineOverride>(e =>
      {
        e.HasKey(o => o.Id);
        e.Property(o => o.DeadlineKey).HasMaxLength(128).IsRequired();
        e.Property(o => o.OriginalValue).HasMaxLength(256);
        e.Property(o => o.OverrideValue).HasMaxLength(256).IsRequired();
        e.Property(o => o.Reason).HasMaxLength(2000).IsRequired();
        e.HasOne(o => o.Case).WithMany(ic => ic.DeadlineOverrides).HasForeignKey(o => o.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(o => o.OverriddenBy).WithMany().HasForeignKey(o => o.OverriddenByUserId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(o => new { o.CaseId, o.DeadlineKey });
      });

    // ONRCFirmRecord (not tenant-scoped — system-wide per region)
    modelBuilder.Entity<ONRCFirmRecord>(e =>
  {
    e.HasKey(f => f.Id);
    e.Property(f => f.CUI).HasMaxLength(64).IsRequired();
    e.Property(f => f.Name).HasMaxLength(512).IsRequired();
    e.Property(f => f.TradeRegisterNo).HasMaxLength(128);
    e.Property(f => f.CAEN).HasMaxLength(32);
    e.Property(f => f.Address).HasMaxLength(512);
    e.Property(f => f.Locality).HasMaxLength(256);
    e.Property(f => f.County).HasMaxLength(256);
    e.Property(f => f.PostalCode).HasMaxLength(16);
    e.Property(f => f.Phone).HasMaxLength(64);
    e.Property(f => f.Status).HasMaxLength(64);
    e.Property(f => f.IncorporationYear).HasMaxLength(10);
    e.Property(f => f.ShareCapitalRon).HasColumnType("decimal(18,2)");
    e.HasIndex(f => f.CUI);
    e.HasIndex(f => f.Name);
    e.HasIndex(f => f.Region);
  });

    // CaseEvent
    modelBuilder.Entity<CaseEvent>(e =>
    {
      e.HasKey(ev => ev.Id);
      e.Property(ev => ev.Category).HasMaxLength(64).IsRequired();
      e.Property(ev => ev.EventType).HasMaxLength(128).IsRequired();
      e.Property(ev => ev.Description).HasMaxLength(2000).IsRequired();
      e.Property(ev => ev.ActorName).HasMaxLength(256);
      e.Property(ev => ev.LinkedEntityType).HasMaxLength(128);
      e.Property(ev => ev.Severity).HasMaxLength(32);
      e.HasOne(ev => ev.Case).WithMany(c => c.Events).HasForeignKey(ev => ev.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasIndex(ev => ev.CaseId);
      e.HasIndex(ev => new { ev.CaseId, ev.OccurredAt });
      e.HasIndex(ev => ev.EventType);
    });

    // AiSystemConfig (system-level, not tenant-scoped)
    modelBuilder.Entity<AiSystemConfig>(e =>
    {
      e.HasKey(a => a.Id);
      e.Property(a => a.Provider).HasMaxLength(64).IsRequired();
      e.Property(a => a.ApiKeyEncrypted).HasMaxLength(2048);
      e.Property(a => a.ApiEndpoint).HasMaxLength(512);
      e.Property(a => a.ModelName).HasMaxLength(128);
      e.Property(a => a.DeploymentName).HasMaxLength(128);
      e.Property(a => a.Notes).HasMaxLength(1000);
    });

    // WorkflowStageDefinition (global or tenant-scoped)
    modelBuilder.Entity<WorkflowStageDefinition>(e =>
    {
      e.HasKey(s => s.Id);
      e.Property(s => s.StageKey).HasMaxLength(64).IsRequired();
      e.Property(s => s.Name).HasMaxLength(256).IsRequired();
      e.Property(s => s.Description).HasMaxLength(2000);
      e.Property(s => s.ApplicableProcedureTypes).HasMaxLength(512);
      e.Property(s => s.RequiredFieldsJson).HasMaxLength(4000);
      e.Property(s => s.RequiredPartyRolesJson).HasMaxLength(4000);
      e.Property(s => s.RequiredDocTypesJson).HasMaxLength(4000);
      e.Property(s => s.RequiredTaskTemplatesJson).HasMaxLength(4000);
      e.Property(s => s.ValidationRulesJson).HasMaxLength(4000);
      e.Property(s => s.OutputDocTypesJson).HasMaxLength(4000);
      e.Property(s => s.OutputTasksJson).HasMaxLength(4000);
      e.Property(s => s.AllowedTransitionsJson).HasMaxLength(4000);
      e.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
      e.HasIndex(s => new { s.TenantId, s.StageKey }).IsUnique();
    });

    // WorkflowStageTemplate (join between stage and template)
    modelBuilder.Entity<WorkflowStageTemplate>(e =>
    {
      e.HasKey(st => st.Id);
      e.Property(st => st.Notes).HasMaxLength(1000);
      e.HasOne(st => st.StageDefinition).WithMany(s => s.Templates).HasForeignKey(st => st.StageDefinitionId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(st => st.DocumentTemplate).WithMany().HasForeignKey(st => st.DocumentTemplateId).OnDelete(DeleteBehavior.Cascade);
    });

    // CreditorClaim
    modelBuilder.Entity<CreditorClaim>(e =>
    {
      e.HasKey(c => c.Id);
      e.Property(c => c.Rank).HasMaxLength(64).IsRequired();
      e.Property(c => c.NatureDescription).HasMaxLength(1000);
      e.Property(c => c.Status).HasMaxLength(32).IsRequired();
      e.Property(c => c.SupportingDocumentIdsJson).HasMaxLength(4000);
      e.Property(c => c.Notes).HasMaxLength(2000);
      e.Property(c => c.DeclaredAmount).HasColumnType("decimal(18,2)");
      e.Property(c => c.AdmittedAmount).HasColumnType("decimal(18,2)");
      e.HasOne(c => c.Case).WithMany(cs => cs.Claims).HasForeignKey(c => c.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(c => c.CreditorParty).WithMany().HasForeignKey(c => c.CreditorPartyId).OnDelete(DeleteBehavior.NoAction);
      e.HasOne(c => c.ReviewedBy).WithMany().HasForeignKey(c => c.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
      e.HasIndex(c => c.CaseId);
    });

    // Asset
    modelBuilder.Entity<Asset>(e =>
    {
      e.HasKey(a => a.Id);
      e.Property(a => a.AssetType).HasMaxLength(64).IsRequired();
      e.Property(a => a.Description).HasMaxLength(2000).IsRequired();
      e.Property(a => a.EncumbranceDetails).HasMaxLength(2000);
      e.Property(a => a.Status).HasMaxLength(32).IsRequired();
      e.Property(a => a.Notes).HasMaxLength(2000);
      e.Property(a => a.EstimatedValue).HasColumnType("decimal(18,2)");
      e.Property(a => a.SaleProceeds).HasColumnType("decimal(18,2)");
      e.HasOne(a => a.Case).WithMany(cs => cs.Assets).HasForeignKey(a => a.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(a => a.SecuredCreditorParty).WithMany().HasForeignKey(a => a.SecuredCreditorPartyId).OnDelete(DeleteBehavior.NoAction);
      e.HasIndex(a => a.CaseId);
    });

    modelBuilder.Entity<CaseWorkflowStage>(e =>
    {
      e.HasKey(s => s.Id);
      e.Property(s => s.StageKey).HasMaxLength(64).IsRequired();
      e.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
      e.Property(s => s.CompletedBy).HasMaxLength(256);
      e.Property(s => s.Notes).HasMaxLength(2000);
      e.HasOne(s => s.Case).WithMany(c => c.WorkflowStages).HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(s => s.StageDefinition).WithMany().HasForeignKey(s => s.StageDefinitionId).OnDelete(DeleteBehavior.NoAction);
      e.HasIndex(s => new { s.CaseId, s.StageKey }).IsUnique();
    });

    modelBuilder.Entity<CaseDeadline>(e =>
    {
      e.HasKey(d => d.Id);
      e.Property(d => d.Label).HasMaxLength(256).IsRequired();
      e.Property(d => d.RelativeTo).HasMaxLength(32).IsRequired();
      e.Property(d => d.PhaseKey).HasMaxLength(64);
      e.Property(d => d.Notes).HasMaxLength(2000);
      e.HasOne(d => d.Case).WithMany().HasForeignKey(d => d.CaseId).OnDelete(DeleteBehavior.Cascade);
      e.HasIndex(d => d.CaseId);
    });

    // ----- Tenant query filters -----
    ApplyTenantQueryFilters(modelBuilder);

    // Fix SQL Server "multiple cascade paths" error: override all TenantId FK
    // delete behaviours from Cascade → Restrict after the query-filter loop has
    // registered them by convention.
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
      foreach (var fk in entityType.GetForeignKeys()
        .Where(fk => fk.Properties.Any(p => p.Name == "TenantId")
                  && fk.DeleteBehavior == DeleteBehavior.Cascade)
        .ToList())
      {
        ((IMutableForeignKey)fk).DeleteBehavior = DeleteBehavior.Restrict;
      }
    }
  }

  private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
  {
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
      if (!typeof(TenantScopedEntity).IsAssignableFrom(entityType.ClrType))
        continue;

      var parameter = Expression.Parameter(entityType.ClrType, "e");
      var tenantIdProperty = Expression.Property(parameter, nameof(TenantScopedEntity.TenantId));

      // Build: e => _currentUser == null || _currentUser.TenantId == null || e.TenantId == _currentUser.TenantId.Value
      // NOTE: GlobalAdmins are NOT bypassed. Their TenantId comes from X-Tenant-Id header
      // (set by TenantResolutionMiddleware). This ensures strict tenant isolation for ALL users.
      var currentUserExpr = Expression.Constant(_currentUser, typeof(ICurrentUserService));
      var nullCheck = Expression.Equal(currentUserExpr, Expression.Constant(null, typeof(ICurrentUserService)));

      var tenantIdAccessor = Expression.Property(currentUserExpr, nameof(ICurrentUserService.TenantId));
      var tenantIdNull = Expression.Equal(tenantIdAccessor, Expression.Constant(null, typeof(Guid?)));

      var tenantIdValue = Expression.Property(tenantIdAccessor, "Value");
      var tenantMatch = Expression.Equal(tenantIdProperty, tenantIdValue);

      // No IsGlobalAdmin bypass — every user, including GlobalAdmin, is filtered by their resolved TenantId
      var body = Expression.OrElse(
        nullCheck,
             Expression.OrElse(tenantIdNull, tenantMatch));

      var filter = Expression.Lambda(body, parameter);
      modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }
  }

  public override int SaveChanges()
  {
    ApplyAuditInfo();
    return base.SaveChanges();
  }

  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    ApplyAuditInfo();
    return await base.SaveChangesAsync(cancellationToken);
  }

  private void ApplyAuditInfo()
  {
    var entries = ChangeTracker.Entries()
        .Where(e => e.Entity is BaseEntity &&
(e.State == EntityState.Added || e.State == EntityState.Modified));

    foreach (var entry in entries)
    {
      var entity = (BaseEntity)entry.Entity;
      var now = DateTime.UtcNow;
      var user = _currentUser?.Email ?? "System";

      if (entry.State == EntityState.Added)
      {
        if (entity.Id == Guid.Empty)
          entity.Id = Guid.NewGuid();
        entity.CreatedOn = now;
        entity.CreatedBy = user;
      }

      entity.LastModifiedOn = now;
      entity.LastModifiedBy = user;

      // Auto-set TenantId for new tenant-scoped entities
      if (entry.State == EntityState.Added && entity is TenantScopedEntity tenantEntity)
      {
        if (tenantEntity.TenantId == Guid.Empty && _currentUser?.TenantId.HasValue == true)
        {
          tenantEntity.TenantId = _currentUser.TenantId.Value;
        }
      }
    }
  }
}
