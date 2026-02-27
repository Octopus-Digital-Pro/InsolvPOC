using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

public sealed class CaseEmailService : ICaseEmailService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CaseEmailService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<EmailDto>> GetByCaseAsync(Guid caseId, string? status, bool? sentOnly, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.ScheduledEmails
           .Where(e => e.CaseId == caseId && (tenantId == null || e.TenantId == tenantId));

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);
        if (sentOnly == true) query = query.Where(e => e.IsSent);

        return await query.OrderByDescending(e => e.ScheduledFor).Select(e => e.ToDto()).ToListAsync(ct);
    }

    public async Task<CaseEmailSummaryResult> GetSummaryAsync(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var emails = await _db.ScheduledEmails
       .Where(e => e.CaseId == caseId && (tenantId == null || e.TenantId == tenantId))
            .ToListAsync(ct);

        return new CaseEmailSummaryResult
        {
            Total = emails.Count,
            Sent = emails.Count(e => e.IsSent),
            Pending = emails.Count(e => !e.IsSent && e.RetryCount < 3),
            Failed = emails.Count(e => !e.IsSent && e.RetryCount >= 3),
            Scheduled = emails.Count(e => !e.IsSent && e.ScheduledFor > DateTime.UtcNow),
        };
    }

    public async Task<EmailDto> ScheduleAsync(Guid caseId, ScheduleEmailCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
 ?? throw new BusinessException("Tenant context is required.");

        if (!await _db.InsolvencyCases.AnyAsync(c => c.Id == caseId && c.TenantId == tenantId, ct))
            throw new BusinessException("Case not found.");

        if (string.IsNullOrWhiteSpace(cmd.To))
            throw new BusinessException("Recipient (To) is required.");

        var email = new ScheduledEmail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            To = cmd.To,
            Cc = cmd.Cc,
            Bcc = cmd.Bcc,
            Subject = cmd.Subject,
            Body = cmd.Body,
            ScheduledFor = cmd.ScheduledFor ?? DateTime.UtcNow,
            Status = "Scheduled",
            RelatedTaskId = cmd.RelatedTaskId,
            RelatedPartyIdsJson = cmd.RelatedPartyIdsJson,
            RelatedDocumentIdsJson = cmd.RelatedDocumentIdsJson,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };

        _db.ScheduledEmails.Add(email);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Email Scheduled for Delivery",
            Description = $"An email to '{cmd.To}' with subject '{cmd.Subject}' was scheduled for delivery.",
            EntityType = "ScheduledEmail",
            EntityId = email.Id,
            EntityName = cmd.Subject,
            Severity = "Info",
            Category = "EmailManagement",
        });

        return email.ToDto();
    }

    public async Task CancelAsync(Guid caseId, Guid emailId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var email = await _db.ScheduledEmails
      .FirstOrDefaultAsync(e => e.Id == emailId && e.CaseId == caseId
      && (tenantId == null || e.TenantId == tenantId), ct)
            ?? throw new BusinessException("Email not found.");

        if (email.IsSent)
            throw new BusinessException("Cannot cancel an already sent email.");

        email.Status = "Cancelled";
        email.LastModifiedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Scheduled Email Cancelled",
            Description = $"A scheduled email to '{email.To}' was cancelled.",
            EntityType = "ScheduledEmail",
            EntityId = emailId,
            EntityName = email.Subject,
            Severity = "Info",
            Category = "EmailManagement",
        });
    }

    public async Task<EmailDto> ComposeAsync(Guid caseId, ComposeEmailCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context is required.");

        if (!await _db.InsolvencyCases.AnyAsync(c => c.Id == caseId && c.TenantId == tenantId, ct))
            throw new BusinessException("Case not found.");

        if (string.IsNullOrWhiteSpace(cmd.Subject))
            throw new BusinessException("Subject is required.");

        // Resolve party emails
        var toAddresses = new List<string>();
        if (cmd.RecipientPartyIds.Count > 0)
        {
            var parties = await _db.CaseParties
                .Include(p => p.Company)
                .Where(p => cmd.RecipientPartyIds.Contains(p.Id) && p.CaseId == caseId)
                .ToListAsync(ct);
            toAddresses.AddRange(parties
                .Select(p => p.Email ?? p.Company?.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!));
        }
        if (!string.IsNullOrWhiteSpace(cmd.ToAddresses))
            toAddresses.AddRange(cmd.ToAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (toAddresses.Count == 0)
            throw new BusinessException("At least one valid recipient email address is required.");

        // Handle thread ID — use InReplyTo's thread, or start a new one
        Guid? threadId = null;
        if (cmd.ReplyToEmailId.HasValue)
        {
            var parent = await _db.ScheduledEmails.FindAsync([cmd.ReplyToEmailId.Value], ct);
            threadId = parent?.ThreadId ?? cmd.ReplyToEmailId;
        }
        threadId ??= Guid.NewGuid();

        // Build document ID json
        var docIdsJson = cmd.AttachedDocumentIds.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(cmd.AttachedDocumentIds)
            : null;

        var email = new ScheduledEmail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            To = string.Join(", ", toAddresses),
            Cc = cmd.Cc,
            Subject = cmd.Subject,
            Body = cmd.Body,
            IsHtml = cmd.IsHtml,
            ScheduledFor = DateTime.UtcNow,
            Status = "Scheduled",
            ThreadId = threadId,
            InReplyToId = cmd.ReplyToEmailId,
            Direction = "Outbound",
            FromName = cmd.FromName ?? _currentUser.Email,
            RelatedTaskId = cmd.RelatedTaskId,
            RelatedPartyIdsJson = cmd.RecipientPartyIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(cmd.RecipientPartyIds)
                : null,
            RelatedDocumentIdsJson = docIdsJson,
            AttachmentsJson = cmd.UploadedAttachmentsJson,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        };

        _db.ScheduledEmails.Add(email);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Email Composed and Queued",
            Description = $"Email '{cmd.Subject}' composed to {toAddresses.Count} recipient(s) for case, queued for delivery.",
            EntityType = "ScheduledEmail",
            EntityId = email.Id,
            EntityName = cmd.Subject,
            Severity = "Info",
            Category = "EmailManagement",
        });

        return email.ToDto();
    }
}

public sealed class BulkEmailService : IBulkEmailService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public BulkEmailService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<BulkEmailResult> SendToCreditorCohortAsync(Guid caseId, BulkEmailCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("Tenant context is required.");

        var caseEntity = await _db.InsolvencyCases
            .Include(c => c.Parties).ThenInclude(p => p.Company)
     .FirstOrDefaultAsync(c => c.Id == caseId && c.TenantId == tenantId, ct)
      ?? throw new BusinessException("Case not found.");

        if (string.IsNullOrWhiteSpace(cmd.Subject) || string.IsNullOrWhiteSpace(cmd.Body))
            throw new BusinessException("Subject and Body are required.");

        var creditorRoles = ResolveRoles(cmd.Roles);
        var recipients = caseEntity.Parties
         .Where(p => creditorRoles.Contains(p.Role))
            .Where(p => !string.IsNullOrWhiteSpace(p.Email ?? p.Company?.Email))
         .ToList();

        if (recipients.Count == 0)
            throw new BusinessException("No eligible recipients with email addresses found.");

        var scheduledFor = cmd.ScheduledFor ?? DateTime.UtcNow;
        var emails = recipients.Select(party => new ScheduledEmail
        {
            TenantId = tenantId,
            CaseId = caseId,
            To = party.Email ?? party.Company?.Email ?? "",
            Cc = cmd.Cc,
            Bcc = cmd.Bcc,
            Subject = ReplacePlaceholders(cmd.Subject, caseEntity, party),
            Body = ReplacePlaceholders(cmd.Body, caseEntity, party),
            ScheduledFor = scheduledFor,
            Status = "Scheduled",
            IsHtml = cmd.IsHtml,
            AttachmentsJson = cmd.AttachmentsJson,
            RelatedPartyIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { party.Id }),
            RelatedTaskId = cmd.RelatedTaskId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
        }).ToList();

        _db.ScheduledEmails.AddRange(emails);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Bulk Email Scheduled to Creditor Cohort",
            Description = $"Bulk email '{cmd.Subject}' scheduled to {emails.Count} creditors for case '{caseEntity.CaseNumber}'.",
            EntityType = "InsolvencyCase",
            EntityId = caseId,
            EntityName = caseEntity.CaseNumber,
            CaseNumber = caseEntity.CaseNumber,
            NewValues = new { recipientCount = emails.Count, cmd.Subject, scheduledFor },
            Severity = "Info",
            Category = "EmailManagement",
        });

        return new BulkEmailResult
        {
            EmailsScheduled = emails.Count,
            ScheduledFor = scheduledFor,
            Recipients = recipients.Select(p => new RecipientInfo
            {
                PartyId = p.Id,
                Name = p.Name ?? p.Company?.Name,
                Email = p.Email ?? p.Company?.Email,
                Role = p.Role.ToString(),
            }).ToList(),
        };
    }

    public async Task<CohortPreviewResult> PreviewCohortAsync(Guid caseId, string? roles, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var caseEntity = await _db.InsolvencyCases
         .Include(c => c.Parties).ThenInclude(p => p.Company)
         .FirstOrDefaultAsync(c => c.Id == caseId && (tenantId == null || c.TenantId == tenantId), ct)
              ?? throw new BusinessException("Case not found.");

        var creditorRoles = ResolveRoles(
 string.IsNullOrWhiteSpace(roles) ? null : roles.Split(',').Select(r => r.Trim()).ToList());

        var recipients = caseEntity.Parties
            .Where(p => creditorRoles.Contains(p.Role))
.Select(p => new CohortRecipient
{
    PartyId = p.Id,
    Name = p.Name ?? p.Company?.Name,
    Email = p.Email ?? p.Company?.Email,
    Role = p.Role.ToString(),
    HasEmail = !string.IsNullOrWhiteSpace(p.Email ?? p.Company?.Email),
}).ToList();

        return new CohortPreviewResult
        {
            Total = recipients.Count,
            WithEmail = recipients.Count(r => r.HasEmail),
            WithoutEmail = recipients.Count(r => !r.HasEmail),
            Recipients = recipients,
        };
    }

    private static HashSet<CasePartyRole> ResolveRoles(List<string>? roleStrings)
    {
        if (roleStrings is { Count: > 0 })
        {
            return roleStrings
                         .Select(r => Enum.TryParse<CasePartyRole>(r, true, out var role) ? role : (CasePartyRole?)null)
            .Where(r => r.HasValue).Select(r => r!.Value)
                .ToHashSet();
        }
        return new HashSet<CasePartyRole>
        {
        CasePartyRole.SecuredCreditor, CasePartyRole.UnsecuredCreditor,
            CasePartyRole.BudgetaryCreditor, CasePartyRole.EmployeeCreditor,
        };
    }

    private static string ReplacePlaceholders(string template, InsolvencyCase cas, CaseParty party)
    {
        return template
                 .Replace("{{CaseNumber}}", cas.CaseNumber)
     .Replace("{{DebtorName}}", cas.DebtorName)
              .Replace("{{RecipientName}}", party.Name ?? party.Company?.Name ?? "")
          .Replace("{{ClaimsDeadline}}", cas.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "N/A")
              .Replace("{{CourtName}}", cas.CourtName ?? "")
       .Replace("{{PractitionerName}}", cas.PractitionerName ?? "");
    }
}
