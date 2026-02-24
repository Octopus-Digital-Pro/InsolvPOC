using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Data;

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
  public DbSet<CasePhase> CasePhases => Set<CasePhase>();
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

        // CasePhase
        modelBuilder.Entity<CasePhase>(e =>
        {
            e.HasKey(p => p.Id);
      e.Property(p => p.Notes).HasMaxLength(4000);
          e.Property(p => p.CourtDecisionRef).HasMaxLength(256);
            e.HasOne(p => p.Case).WithMany(c => c.Phases).HasForeignKey(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.CaseId, p.PhaseType }).IsUnique();
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
   e.HasOne(g => g.Case).WithMany(ic => ic.GeneratedLetters).HasForeignKey(g => g.CaseId).OnDelete(DeleteBehavior.NoAction);
 e.HasOne(g => g.Template).WithMany().HasForeignKey(g => g.TemplateId).OnDelete(DeleteBehavior.SetNull);
     // Use string FK overload with no navigation — GeneratedLetter has no Tenant nav property.
     // This overrides the default Cascade on TenantId to NoAction to break the cascade cycle.
     e.HasOne<Tenant>().WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.NoAction).IsRequired();
   e.HasIndex(g => new { g.CaseId, g.TemplateType });
        });

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
    e.Property(f => e.TradeRegisterNo).HasMaxLength(128);
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

    // ----- Tenant query filters -----
    ApplyTenantQueryFilters(modelBuilder);
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
