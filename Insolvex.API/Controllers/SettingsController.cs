using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using System.Security.Cryptography;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public SettingsController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
      _db = db;
        _currentUser = currentUser;
 _audit = audit;
    }

    // ?? Tenant Settings ??

    [HttpGet("tenant")]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetTenantSettings()
    {
        if (!_currentUser.TenantId.HasValue) return BadRequest(new { message = "No tenant context" });

    var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId.Value);
      if (tenant == null) return NotFound();

 return Ok(new
     {
     tenant.Id,
    tenant.Name,
        tenant.Domain,
       tenant.IsActive,
     tenant.PlanName,
            tenant.SubscriptionExpiry,
    region = tenant.Region.ToString(),
 userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenant.Id),
       companyCount = await _db.Companies.CountAsync(),
      caseCount = await _db.InsolvencyCases.CountAsync()
      });
    }

    [HttpPut("tenant")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpdateTenantSettings([FromBody] UpdateTenantSettingsRequest request)
    {
   if (!_currentUser.TenantId.HasValue) return BadRequest(new { message = "No tenant context" });

    var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId.Value);
        if (tenant == null) return NotFound();

        var oldValues = new { tenant.Name, tenant.Domain, Region = tenant.Region.ToString() };

   if (request.Name != null) tenant.Name = request.Name;
if (request.Domain != null) tenant.Domain = request.Domain;
     if (request.Region != null && Enum.TryParse<SystemRegion>(request.Region, true, out var region))
    tenant.Region = region;

     await _db.SaveChangesAsync();
     await _audit.LogEntityAsync("Settings.TenantUpdated", "Tenant", tenant.Id,
   oldValues, new { tenant.Name, tenant.Domain, Region = tenant.Region.ToString() });
    return Ok(new { message = "Settings updated" });
    }

    // ?? Scheduled Emails ??

    [HttpGet("emails")]
    [RequirePermission(Permission.EmailView)]
    public async Task<IActionResult> GetScheduledEmails(
    [FromQuery] bool? sent = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.ScheduledEmails.AsQueryable();
        if (sent.HasValue) query = query.Where(e => e.IsSent == sent.Value);

        var total = await query.CountAsync();
var emails = await query
     .OrderByDescending(e => e.ScheduledFor)
     .Skip(page * pageSize)
.Take(pageSize)
            .Select(e => new
      {
         e.Id,
   e.To,
   e.Cc,
         e.Subject,
       e.ScheduledFor,
        e.SentAt,
       e.IsSent,
       e.RetryCount,
             e.ErrorMessage
     })
            .ToListAsync();

    return Ok(new { total, page, pageSize, items = emails });
    }

    [HttpPost("emails")]
 [RequirePermission(Permission.EmailCreate)]
    public async Task<IActionResult> CreateScheduledEmail([FromBody] CreateScheduledEmailRequest request)
    {
        var email = new ScheduledEmail
        {
   Id = Guid.NewGuid(),
    To = request.To,
            Cc = request.Cc,
            Subject = request.Subject,
        Body = request.Body,
ScheduledFor = request.ScheduledFor ?? DateTime.UtcNow
        };

    _db.ScheduledEmails.Add(email);
    await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("Email.Scheduled", "ScheduledEmail", email.Id,
    newValues: new { email.To, email.Subject, email.ScheduledFor });

        return Ok(new { id = email.Id, message = "Email scheduled" });
    }

  [HttpDelete("emails/{id:guid}")]
    [RequirePermission(Permission.EmailDelete)]
 public async Task<IActionResult> DeleteScheduledEmail(Guid id)
    {
        var email = await _db.ScheduledEmails.FirstOrDefaultAsync(e => e.Id == id);
        if (email == null) return NotFound();
        if (email.IsSent) return BadRequest(new { message = "Cannot delete a sent email" });

        await _audit.LogEntityAsync("Email.Deleted", "ScheduledEmail", id,
            oldValues: new { email.To, email.Subject, email.ScheduledFor }, severity: "Warning");

  _db.ScheduledEmails.Remove(email);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }

    // ?? System Config (global) ??

    [HttpGet("config")]
    [RequirePermission(Permission.SystemConfigView)]
    public async Task<IActionResult> GetSystemConfig([FromQuery] string? group = null)
    {
        var query = _db.SystemConfigs.IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrWhiteSpace(group))
      query = query.Where(c => c.Group == group);

    var configs = await query.OrderBy(c => c.Group).ThenBy(c => c.Key)
   .Select(c => new { c.Id, c.Key, c.Value, c.Description, c.Group })
     .ToListAsync();
        return Ok(configs);
    }

    [HttpPut("config")]
    [RequirePermission(Permission.SystemConfigEdit)]
    public async Task<IActionResult> UpdateSystemConfig([FromBody] UpdateSystemConfigRequest request)
  {
      foreach (var item in request.Items)
        {
            var config = await _db.SystemConfigs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Key == item.Key);
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
        await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("Settings.ConfigUpdated", "SystemConfig", Guid.Empty,
            newValues: new { itemCount = request.Items.Count, keys = request.Items.Select(i => i.Key).ToList() },
          severity: "Critical");
        return Ok(new { message = "Configuration updated. Restart the application for storage provider changes to take effect." });
    }

    // ?? Demo Reset ??

    [HttpPost("demo/reset")]
    [RequirePermission(Permission.DemoReset)]
    public async Task<IActionResult> DemoReset()
    {
        // Preserve: tenants, users, system config, insolvency firms
        // Delete everything else in correct FK order

        // 1. Scheduled emails
        var emails = await _db.ScheduledEmails.IgnoreQueryFilters().ToListAsync();
        _db.ScheduledEmails.RemoveRange(emails);

        // 2. Pending uploads
        var uploads = await _db.PendingUploads.IgnoreQueryFilters().ToListAsync();
        _db.PendingUploads.RemoveRange(uploads);

        // 3. Case phases
      var phases = await _db.CasePhases.IgnoreQueryFilters().ToListAsync();
    _db.CasePhases.RemoveRange(phases);

   // 4. Case parties
  var parties = await _db.CaseParties.IgnoreQueryFilters().ToListAsync();
        _db.CaseParties.RemoveRange(parties);

        // 5. Insolvency documents
        var docs = await _db.InsolvencyDocuments.IgnoreQueryFilters().ToListAsync();
     _db.InsolvencyDocuments.RemoveRange(docs);

        // 6. Company tasks
        var tasks = await _db.CompanyTasks.IgnoreQueryFilters().ToListAsync();
        _db.CompanyTasks.RemoveRange(tasks);

// 7. Insolvency cases
        var cases = await _db.InsolvencyCases.IgnoreQueryFilters().ToListAsync();
      _db.InsolvencyCases.RemoveRange(cases);

        // 8. Companies
      var companies = await _db.Companies.IgnoreQueryFilters().ToListAsync();
        _db.Companies.RemoveRange(companies);

        // 9. Audit logs
        var auditLogs = await _db.AuditLogs.IgnoreQueryFilters().ToListAsync();
        _db.AuditLogs.RemoveRange(auditLogs);

   // 10. Error logs
        var errorLogs = await _db.ErrorLogs.IgnoreQueryFilters().ToListAsync();
      _db.ErrorLogs.RemoveRange(errorLogs);

     await _db.SaveChangesAsync();

        await _audit.LogAsync(new AuditEntry
 {
Action = "Settings.DemoReset",
    Category = "Settings",
      Severity = "Critical",
        NewValues = new { scheduledEmails = emails.Count, cases = cases.Count, companies = companies.Count },
        });

    return Ok(new
        {
     message = "Demo reset complete. Tenants, users, system config, and firm settings preserved.",
          deleted = new
       {
           scheduledEmails = emails.Count,
      pendingUploads = uploads.Count,
              casePhases = phases.Count,
      caseParties = parties.Count,
    documents = docs.Count,
tasks = tasks.Count,
      cases = cases.Count,
                companies = companies.Count,
     auditLogs = auditLogs.Count,
                errorLogs = errorLogs.Count,
            }
     });
    }

    // ?? Error Logs ??

    [HttpGet("errors")]
    [RequirePermission(Permission.ErrorLogView)]
    public async Task<IActionResult> GetErrorLogs(
        [FromQuery] bool? resolved = null,
  [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20)
    {
     var query = _db.ErrorLogs.AsQueryable();
        if (resolved.HasValue) query = query.Where(e => e.IsResolved == resolved.Value);

        var total = await query.CountAsync();
        var logs = await query
     .OrderByDescending(e => e.Timestamp)
            .Skip(page * pageSize)
  .Take(pageSize)
     .Select(e => new
            {
      e.Id,
   e.Message,
        e.Source,
    e.RequestPath,
        e.RequestMethod,
           e.UserEmail,
           e.Timestamp,
          e.IsResolved
          })
        .ToListAsync();

        return Ok(new { total, page, pageSize, items = logs });
    }

    [HttpPut("errors/{id:guid}/resolve")]
    [RequirePermission(Permission.ErrorLogResolve)]
    public async Task<IActionResult> ResolveError(Guid id)
    {
        var log = await _db.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id);
        if (log == null) return NotFound();

        log.IsResolved = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Marked as resolved" });
  }

    // ?? Users (admin) ??

    [HttpGet("users")]
    [RequirePermission(Permission.UserView)]
    public async Task<IActionResult> GetTenantUsers()
    {
      var users = await _db.Users
            .Select(u => new
       {
      u.Id,
     u.Email,
                u.FirstName,
    u.LastName,
       FullName = u.FirstName + " " + u.LastName,
        u.Role,
          u.IsActive,
          u.LastLoginDate,
      u.CreatedOn
     })
  .OrderBy(u => u.FirstName)
     .ToListAsync();

        return Ok(users);
    }

    // ?? Insolvency Firm (licensee details) ??

    [HttpGet("firm")]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetFirm()
    {
   if (!_currentUser.TenantId.HasValue) return BadRequest(new { message = "No tenant context" });

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync(f => f.TenantId == _currentUser.TenantId.Value);
     if (firm == null) return Ok((InsolvencyFirmDto?)null);
     return Ok(firm.ToDto());
    }

    [HttpPut("firm")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> UpsertFirm([FromBody] UpsertInsolvencyFirmRequest request)
    {
        if (!_currentUser.TenantId.HasValue) return BadRequest(new { message = "No tenant context" });

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync(f => f.TenantId == _currentUser.TenantId.Value);

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

        await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("Settings.FirmUpdated", "InsolvencyFirm", firm.Id,
  newValues: new { firm.FirmName, firm.CuiRo, firm.UnpirRegistrationNo });
      return Ok(firm.ToDto());
    }

    // ?? Document Templates (global + tenant override) ??

    [HttpGet("templates")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetTemplates()
    {
    var tenantId = _currentUser.TenantId;

      // Get all global templates + tenant-specific overrides
        var templates = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == null || t.TenantId == tenantId)
  .OrderBy(t => t.TemplateType)
      .ThenByDescending(t => t.TenantId) // tenant overrides first
 .Select(t => new
  {
                t.Id,
              t.TenantId,
       IsGlobal = t.TenantId == null,
   TemplateType = t.TemplateType.ToString(),
     t.Name,
         t.FileName,
t.ContentType,
                t.FileSizeBytes,
        t.Description,
    t.Stage,
      t.IsActive,
    t.Version,
          t.CreatedOn,
  t.CreatedBy,
  })
            .ToListAsync();

      return Ok(templates);
 }

    [HttpPost("templates/upload")]
    [RequirePermission(Permission.TemplateManage)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadTemplate(
      IFormFile file,
        [FromForm] string templateType,
        [FromForm] string? name = null,
        [FromForm] string? description = null,
   [FromForm] string? stage = null,
        [FromForm] bool global = false)
    {
        if (file == null || file.Length == 0)
    return BadRequest(new { message = "No file provided" });

        if (!Enum.TryParse<DocumentTemplateType>(templateType, true, out var tt))
            return BadRequest(new { message = $"Unknown template type: {templateType}" });

        // GlobalAdmin can upload global templates; TenantAdmin only tenant-specific
        Guid? targetTenantId;
        if (global)
        {
   if (!_currentUser.IsGlobalAdmin)
             return Forbid();
          targetTenantId = null;
        }
        else
   {
            targetTenantId = _currentUser.TenantId;
        if (!targetTenantId.HasValue)
       return BadRequest(new { message = "No tenant context" });
     }

        // Compute file hash
        using var hashStream = file.OpenReadStream();
        var hashBytes = await SHA256.HashDataAsync(hashStream);
  var fileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Upload to storage
        var storageKey = $"templates/{(targetTenantId.HasValue ? targetTenantId.Value.ToString() : "global")}/{tt}/{file.FileName}";
        var storage = HttpContext.RequestServices.GetRequiredService<IFileStorageService>();
 using var uploadStream = file.OpenReadStream();
        await storage.UploadAsync(storageKey, uploadStream, file.ContentType);

        // Upsert: replace existing template for this type+tenant
        var existing = await _db.DocumentTemplates
            .IgnoreQueryFilters()
    .FirstOrDefaultAsync(t => t.TenantId == targetTenantId && t.TemplateType == tt);

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

        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("Template.Uploaded", "DocumentTemplate", existing.Id,
   newValues: new { templateType = tt.ToString(), existing.FileName, isGlobal = targetTenantId == null, existing.Version });

  return Ok(new
        {
          id = existing.Id,
            templateType = tt.ToString(),
       fileName = existing.FileName,
            isGlobal = targetTenantId == null,
        version = existing.Version,
            message = "Template uploaded",
 });
    }

    [HttpDelete("templates/{id:guid}")]
    [RequirePermission(Permission.TemplateManage)]
  public async Task<IActionResult> DeleteTemplate(Guid id)
    {
    var template = await _db.DocumentTemplates
     .IgnoreQueryFilters()
         .FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound();

        // TenantAdmin can only delete their own tenant templates
        if (template.TenantId == null && !_currentUser.IsGlobalAdmin)
     return Forbid();
        if (template.TenantId != null && template.TenantId != _currentUser.TenantId)
  return Forbid();

        await _audit.LogEntityAsync("Template.Deleted", "DocumentTemplate", id,
   oldValues: new { templateType = template.TemplateType.ToString(), template.FileName },
      severity: "Warning");

  _db.DocumentTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Template deleted" });
    }

    [HttpGet("templates/{id:guid}/download")]
    [RequirePermission(Permission.TemplateView)]
 public async Task<IActionResult> DownloadTemplate(Guid id)
    {
        var template = await _db.DocumentTemplates
          .IgnoreQueryFilters()
     .FirstOrDefaultAsync(t => t.Id == id);
      if (template == null) return NotFound();

    var storage = HttpContext.RequestServices.GetRequiredService<IFileStorageService>();
        if (!await storage.ExistsAsync(template.StorageKey))
      return NotFound("Template file not found in storage");

   var stream = await storage.DownloadAsync(template.StorageKey);
 return File(stream, template.ContentType, template.FileName);
    }
}

public record UpdateSystemConfigRequest(List<SystemConfigItem> Items);
public record SystemConfigItem(string Key, string Value, string? Description = null, string? Group = null);

public record UpdateTenantSettingsRequest(string? Name = null, string? Domain = null, string? Region = null);

public record CreateScheduledEmailRequest(
    string To,
    string Subject,
    string Body,
string? Cc = null,
    DateTime? ScheduledFor = null
);

public record InsolvencyFirmDto
{
    public string FirmName { get; init; } = null!;
    public string CuiRo { get; init; } = null!;
    public string TradeRegisterNo { get; init; } = null!;
    public string VatNumber { get; init; } = null!;
    public string UnpirRegistrationNo { get; init; } = null!;
    public string UnpirRfo { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string Locality { get; init; } = null!;
    public string County { get; init; } = null!;
    public string Country { get; init; } = null!;
    public string PostalCode { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string Fax { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Website { get; init; } = null!;
    public string ContactPerson { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public string BankName { get; init; } = null!;
    public string? SecondaryIban { get; init; }
    public string? SecondaryBankName { get; init; }
}

public record UpsertInsolvencyFirmRequest
{
    public string FirmName { get; init; } = null!;
    public string CuiRo { get; init; } = null!;
    public string TradeRegisterNo { get; init; } = null!;
    public string VatNumber { get; init; } = null!;
    public string UnpirRegistrationNo { get; init; } = null!;
    public string UnpirRfo { get; init; } = null!;
    public string Address { get; init; } = null!;
    public string Locality { get; init; } = null!;
    public string County { get; init; } = null!;
    public string Country { get; init; } = null!;
    public string PostalCode { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string Fax { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Website { get; init; } = null!;
    public string ContactPerson { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public string BankName { get; init; } = null!;
    public string? SecondaryIban { get; init; }
    public string? SecondaryBankName { get; init; }
}
