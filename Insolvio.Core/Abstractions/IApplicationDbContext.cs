using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Abstraction over the EF Core DbContext, allowing service implementations in
/// Insolvio.Core to access data without a hard dependency on the SQL Server
/// infrastructure in Insolvio.Data.
/// </summary>
public interface IApplicationDbContext
{
    // ----- Entity sets -----
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<UserInvitation> UserInvitations { get; }
    DbSet<Company> Companies { get; }
    DbSet<InsolvencyCase> InsolvencyCases { get; }
    DbSet<InsolvencyDocument> InsolvencyDocuments { get; }
    DbSet<CompanyTask> CompanyTasks { get; }
    DbSet<CaseParty> CaseParties { get; }
    DbSet<InsolvencyFirm> InsolvencyFirms { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ErrorLog> ErrorLogs { get; }
    DbSet<ScheduledEmail> ScheduledEmails { get; }
    DbSet<PendingUpload> PendingUploads { get; }
    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<DocumentTemplate> DocumentTemplates { get; }
    DbSet<UserSigningKey> UserSigningKeys { get; }
    DbSet<DigitalSignature> DigitalSignatures { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<CaseSummary> CaseSummaries { get; }
    DbSet<Tribunal> Tribunals { get; }
    DbSet<FinanceAuthority> FinanceAuthorities { get; }
    DbSet<LocalGovernment> LocalGovernments { get; }
    DbSet<GeneratedLetter> GeneratedLetters { get; }
    DbSet<TenantDeadlineSettings> TenantDeadlineSettings { get; }
    DbSet<CaseDeadlineOverride> CaseDeadlineOverrides { get; }
    DbSet<ONRCFirmRecord> ONRCFirmRecords { get; }
    DbSet<CaseEvent> CaseEvents { get; }
    DbSet<AiSystemConfig> AiSystemConfigs { get; }
    DbSet<WorkflowStageDefinition> WorkflowStageDefinitions { get; }
    DbSet<WorkflowStageTemplate> WorkflowStageTemplates { get; }
    DbSet<CreditorClaim> CreditorClaims { get; }
    DbSet<Asset> Assets { get; }
    DbSet<CaseWorkflowStage> CaseWorkflowStages { get; }
    DbSet<CaseDeadline> CaseDeadlines { get; }
    DbSet<TaskNote> TaskNotes { get; }
    DbSet<IncomingDocumentProfile> IncomingDocumentProfiles { get; }
    DbSet<TenantAiConfig> TenantAiConfigs { get; }
    DbSet<AiChatMessage> AiChatMessages { get; }
    DbSet<AiCorrectionFeedback> AiCorrectionFeedbacks { get; }
    DbSet<Notification> Notifications { get; }

    // ----- DbContext methods used by services -----
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    ChangeTracker ChangeTracker { get; }
}
