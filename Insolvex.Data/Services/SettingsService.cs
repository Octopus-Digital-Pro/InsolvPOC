using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly MailMergeService _mailMerge;
    private readonly IFileStorageService _storage;

    public SettingsService(
        ApplicationDbContext db, ICurrentUserService currentUser,
        IAuditService audit, MailMergeService mailMerge, IFileStorageService storage)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _mailMerge = mailMerge;
        _storage = storage;
    }

    // — Tenant ——————————————————————————————————————————————

    public async Task<object> GetTenantAsync(CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId.Value, ct)
            ?? throw new NotFoundException("Tenant", _currentUser.TenantId.Value);

        return new
        {
            tenant.Id,
            tenant.Name,
            tenant.Domain,
            tenant.IsActive,
            tenant.PlanName,
            tenant.SubscriptionExpiry,
            region = tenant.Region.ToString(),
            language = tenant.Language,
            userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenant.Id, ct),
            companyCount = await _db.Companies.CountAsync(ct),
            caseCount = await _db.InsolvencyCases.CountAsync(ct),
        };
    }

    public async Task UpdateTenantAsync(UpdateTenantSettingsRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId.Value, ct)
            ?? throw new NotFoundException("Tenant", _currentUser.TenantId.Value);

        var old = new { tenant.Name, tenant.Domain, Region = tenant.Region.ToString() };

        if (request.Name != null) tenant.Name = request.Name;
        if (request.Domain != null) tenant.Domain = request.Domain;
        if (request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var region))
            tenant.Region = region;
        if (request.Language != null) tenant.Language = request.Language;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Tenant Settings Updated", "Tenant", tenant.Id,
            old, new { tenant.Name, tenant.Domain, Region = tenant.Region.ToString() });
    }

    // — Scheduled Emails ————————————————————————————————————

    public async Task<(List<ScheduledEmailDto> Items, int Total)> GetScheduledEmailsAsync(bool? sent, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.ScheduledEmails.AsQueryable();
        if (sent.HasValue) query = query.Where(e => e.IsSent == sent.Value);
        var total = await query.CountAsync(ct);
        var emails = await query
            .OrderByDescending(e => e.ScheduledFor)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(e => new ScheduledEmailDto(e.Id, e.To, e.Cc, e.Subject, e.Body, e.ScheduledFor, e.SentAt, e.IsSent, e.Status, e.ErrorMessage, e.CreatedOn))
            .ToListAsync(ct);
        return (emails, total);
    }

    public async Task<object> CreateScheduledEmailAsync(CreateScheduledEmailRequest request, CancellationToken ct = default)
    {
        var email = new ScheduledEmail
        {
            Id = Guid.NewGuid(),
            To = request.To,
            Cc = request.Cc,
            Subject = request.Subject,
            Body = request.Body,
            ScheduledFor = request.ScheduledFor ?? DateTime.UtcNow,
        };
        _db.ScheduledEmails.Add(email);
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Email Scheduled", "ScheduledEmail", email.Id,
            newValues: new { email.To, email.Subject, email.ScheduledFor });
        return new { id = email.Id, message = "Email scheduled" };
    }

    public async Task DeleteScheduledEmailAsync(Guid id, CancellationToken ct = default)
    {
        var email = await _db.ScheduledEmails.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("ScheduledEmail", id);
        if (email.IsSent) throw new BusinessException("Cannot delete a sent email");
        await _audit.LogEntityAsync("Scheduled Email Deleted", "ScheduledEmail", id,
            oldValues: new { email.To, email.Subject, email.ScheduledFor }, severity: "Warning");
        _db.ScheduledEmails.Remove(email);
        await _db.SaveChangesAsync(ct);
    }

    // — System Config ——————————————————————————————————————

    public async Task<List<SystemConfigDto>> GetSystemConfigAsync(string? group, CancellationToken ct = default)
    {
        var query = _db.SystemConfigs.IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrWhiteSpace(group)) query = query.Where(c => c.Group == group);
        return await query.OrderBy(c => c.Group).ThenBy(c => c.Key)
            .Select(c => c.ToDto())
            .ToListAsync(ct);
    }

    public async Task UpdateSystemConfigAsync(UpdateSystemConfigRequest request, CancellationToken ct = default)
    {
        foreach (var item in request.Items)
        {
            var config = await _db.SystemConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Key == item.Key, ct);
            if (config != null)
            {
                config.Value = item.Value;
            }
            else
            {
                _db.SystemConfigs.Add(new SystemConfig
                {
                    Key = item.Key,
                    Value = item.Value,
                    Description = item.Description,
                    Group = item.Group,
                });
            }
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("System Configuration Updated", "SystemConfig", Guid.Empty,
            newValues: new { itemCount = request.Items.Count, keys = request.Items.Select(i => i.Key).ToList() },
            severity: "Critical");
    }

    // — Demo Reset ——————————————————————————————————————————

    public async Task<object> DemoResetAsync(CancellationToken ct = default)
    {
        var emails = await _db.ScheduledEmails.IgnoreQueryFilters().ToListAsync(ct);
        _db.ScheduledEmails.RemoveRange(emails);
        var uploads = await _db.PendingUploads.IgnoreQueryFilters().ToListAsync(ct);
        _db.PendingUploads.RemoveRange(uploads);
        var parties = await _db.CaseParties.IgnoreQueryFilters().ToListAsync(ct);
        _db.CaseParties.RemoveRange(parties);
        var docs = await _db.InsolvencyDocuments.IgnoreQueryFilters().ToListAsync(ct);
        _db.InsolvencyDocuments.RemoveRange(docs);
        var tasks = await _db.CompanyTasks.IgnoreQueryFilters().ToListAsync(ct);
        _db.CompanyTasks.RemoveRange(tasks);
        var cases = await _db.InsolvencyCases.IgnoreQueryFilters().ToListAsync(ct);
        _db.InsolvencyCases.RemoveRange(cases);
        var companies = await _db.Companies.IgnoreQueryFilters().ToListAsync(ct);
        _db.Companies.RemoveRange(companies);
        var auditLogs = await _db.AuditLogs.IgnoreQueryFilters().ToListAsync(ct);
        _db.AuditLogs.RemoveRange(auditLogs);
        var errorLogs = await _db.ErrorLogs.IgnoreQueryFilters().ToListAsync(ct);
        _db.ErrorLogs.RemoveRange(errorLogs);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Demo Data Reset Performed",
            Category = "Settings",
            Severity = "Critical",
            NewValues = new { scheduledEmails = emails.Count, cases = cases.Count, companies = companies.Count },
        });

        return new
        {
            message = "Demo reset complete. Tenants, users, system config, and firm settings preserved.",
            deleted = new
            {
                scheduledEmails = emails.Count,
                pendingUploads = uploads.Count,
                caseParties = parties.Count,
                documents = docs.Count,
                tasks = tasks.Count,
                cases = cases.Count,
                companies = companies.Count,
                auditLogs = auditLogs.Count,
                errorLogs = errorLogs.Count,
            },
        };
    }

    // — Firm ————————————————————————————————————————————————

    public async Task<InsolvencyFirmDto?> GetFirmAsync(CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");
        var firm = await _db.InsolvencyFirms
            .FirstOrDefaultAsync(f => f.TenantId == _currentUser.TenantId.Value, ct);
        return firm?.ToDto();
    }

    public async Task<InsolvencyFirmDto> UpsertFirmAsync(UpsertInsolvencyFirmRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.TenantId.HasValue) throw new BusinessException("No tenant context");
        var firm = await _db.InsolvencyFirms
            .FirstOrDefaultAsync(f => f.TenantId == _currentUser.TenantId.Value, ct);

        if (firm == null)
        {
            firm = new InsolvencyFirm { TenantId = _currentUser.TenantId.Value };
            _db.InsolvencyFirms.Add(firm);
        }

        firm.FirmName = request.FirmName;
        firm.CuiRo = request.CuiRo;
        firm.TradeRegisterNo = request.TradeRegisterNo;
        firm.VatNumber = request.VatNumber;
        firm.UnpirRegistrationNo = request.UnpirRegistrationNo;
        firm.UnpirRfo = request.UnpirRfo;
        firm.Address = request.Address;
        firm.Locality = request.Locality;
        firm.County = request.County;
        firm.Country = request.Country;
        firm.PostalCode = request.PostalCode;
        firm.Phone = request.Phone;
        firm.Fax = request.Fax;
        firm.Email = request.Email;
        firm.Website = request.Website;
        firm.ContactPerson = request.ContactPerson;
        firm.Iban = request.Iban;
        firm.BankName = request.BankName;
        firm.SecondaryIban = request.SecondaryIban;
        firm.SecondaryBankName = request.SecondaryBankName;

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Insolvency Firm Details Updated", "InsolvencyFirm", firm.Id,
            newValues: new { firm.FirmName, firm.CuiRo, firm.UnpirRegistrationNo });
        return firm.ToDto();
    }

    // — Templates ——————————————————————————————————————————

    public async Task<List<object>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await _mailMerge.GetAvailableTemplatesAsync(_currentUser.TenantId, ct);
        return templates.Cast<object>().ToList();
    }

    public async Task<object> UploadTemplateAsync(IFormFile file, string templateType, string? name,
        string? description, string? stage, bool global, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DocumentTemplateType>(templateType, true, out var tt))
            throw new BusinessException($"Unknown template type: {templateType}");

        Guid? targetTenantId;
        if (global)
        {
            if (!_currentUser.IsGlobalAdmin) throw new ForbiddenException("Only GlobalAdmin may upload global templates.");
            targetTenantId = null;
        }
        else
        {
            targetTenantId = _currentUser.TenantId ?? throw new BusinessException("No tenant context");
        }

        using var hashStream = file.OpenReadStream();
        var hashBytes = await SHA256.HashDataAsync(hashStream, ct);
        var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var storageKey = $"templates/{(targetTenantId.HasValue ? targetTenantId.Value.ToString() : "global")}/{tt}/{file.FileName}";
        using var uploadStream = file.OpenReadStream();
        await _storage.UploadAsync(storageKey, uploadStream, file.ContentType);

        var existing = await _db.DocumentTemplates.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == targetTenantId && t.TemplateType == tt, ct);

        if (existing != null)
        {
            existing.FileName = file.FileName;
            existing.StorageKey = storageKey;
            existing.ContentType = file.ContentType;
            existing.FileSizeBytes = file.Length;
            existing.FileHash = fileHash;
            existing.Name = name ?? existing.Name;
            existing.Description = description ?? existing.Description;
            existing.Stage = stage ?? existing.Stage;
            existing.Version++;
        }
        else
        {
            existing = new DocumentTemplate
            {
                TenantId = targetTenantId,
                TemplateType = tt,
                Name = name ?? $"{tt} Template",
                FileName = file.FileName,
                StorageKey = storageKey,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                FileHash = fileHash,
                Description = description,
                Stage = stage,
            };
            _db.DocumentTemplates.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogEntityAsync("Document Template Uploaded", "DocumentTemplate", existing.Id,
            newValues: new { templateType = tt.ToString(), existing.FileName, isGlobal = targetTenantId == null, existing.Version });

        return new
        {
            id = existing.Id,
            templateType = tt.ToString(),
            fileName = existing.FileName,
            isGlobal = targetTenantId == null,
            version = existing.Version,
            message = "Template uploaded",
        };
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.DocumentTemplates.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("DocumentTemplate", id);

        if (template.TenantId == null && !_currentUser.IsGlobalAdmin)
            throw new ForbiddenException("Only GlobalAdmin may delete global templates.");
        if (template.TenantId != null && template.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("Cannot delete another tenant's template.");

        await _audit.LogEntityAsync("Document Template Deleted", "DocumentTemplate", id,
            oldValues: new { templateType = template.TemplateType.ToString(), template.FileName }, severity: "Warning");
        _db.DocumentTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)> DownloadTemplateAsync(
        Guid id, CancellationToken ct = default)
    {
        var template = await _db.DocumentTemplates.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("DocumentTemplate", id);

        if (!await _storage.ExistsAsync(template.StorageKey))
            throw new NotFoundException("Template file not found in storage");

        var stream = await _storage.DownloadAsync(template.StorageKey);
        return (stream, template.FileName, template.ContentType);
    }
}
