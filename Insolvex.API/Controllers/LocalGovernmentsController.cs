using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/local-governments")]
[Authorize]
public class LocalGovernmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
  private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public LocalGovernmentsController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
  _audit = audit;
    }

    [HttpGet]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<LocalGovernment> query = _db.LocalGovernments.AsNoTracking().IgnoreQueryFilters();
   if (!_currentUser.IsGlobalAdmin)
          query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        var items = await query
   .OrderBy(t => t.County).ThenBy(t => t.Name)
      .Select(t => new
            {
 t.Id, t.TenantId, t.Name, t.Locality, t.County, t.Address, t.PostalCode,
t.Phone, t.Fax, t.Email, t.Website, t.ContactPerson, t.ScheduleHours, t.Notes,
         t.OverridesGlobalId,
     IsGlobal = t.TenantId == null,
           IsTenantOverride = t.TenantId != null,
})
            .ToListAsync();

     return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _db.LocalGovernments.AsNoTracking().IgnoreQueryFilters()
  .FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
   if (!_currentUser.IsGlobalAdmin && item.TenantId != null && item.TenantId != _currentUser.TenantId)
            return Forbid();
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Create([FromBody] LocalGovernmentRequest request)
    {
        var item = new LocalGovernment
        {
            Id = Guid.NewGuid(),
       TenantId = _currentUser.IsGlobalAdmin ? null : _currentUser.TenantId,
Name = request.Name,
            Locality = request.Locality,
            County = request.County,
  Address = request.Address,
            PostalCode = request.PostalCode,
   Phone = request.Phone,
      Fax = request.Fax,
       Email = request.Email,
         Website = request.Website,
  ContactPerson = request.ContactPerson,
            ScheduleHours = request.ScheduleHours,
        Notes = request.Notes,
        OverridesGlobalId = request.OverridesGlobalId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
      };

   _db.LocalGovernments.Add(item);
        await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("LocalGov.Created", "LocalGovernment", item.Id,
     newValues: new { item.Name, item.County, IsGlobal = item.TenantId == null });

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] LocalGovernmentRequest request)
    {
   var item = await _db.LocalGovernments.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
        if (!_currentUser.IsGlobalAdmin && item.TenantId != _currentUser.TenantId)
            return Forbid();

        var oldValues = new { item.Name, item.County, item.Phone, item.Email };

        item.Name = request.Name;
        item.Locality = request.Locality;
   item.County = request.County;
        item.Address = request.Address;
        item.PostalCode = request.PostalCode;
      item.Phone = request.Phone;
        item.Fax = request.Fax;
        item.Email = request.Email;
        item.Website = request.Website;
        item.ContactPerson = request.ContactPerson;
     item.ScheduleHours = request.ScheduleHours;
  item.Notes = request.Notes;
 item.OverridesGlobalId = request.OverridesGlobalId;

        await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("LocalGov.Updated", "LocalGovernment", item.Id, oldValues,
new { item.Name, item.County, item.Phone, item.Email });

        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Delete(Guid id)
    {
    var item = await _db.LocalGovernments.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (item == null) return NotFound();
        if (!_currentUser.IsGlobalAdmin && item.TenantId != _currentUser.TenantId)
    return Forbid();

        _db.LocalGovernments.Remove(item);
    await _db.SaveChangesAsync();
        await _audit.LogEntityAsync("LocalGov.Deleted", "LocalGovernment", item.Id,
   oldValues: new { item.Name, item.County }, severity: "Warning");

        return NoContent();
    }

    [HttpPost("import-csv")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var imported = 0;
        var errors = new List<string>();

    try
      {
   using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
     using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
            var records = csv.GetRecords<AuthorityCsvRow>().ToList();

            foreach (var row in records)
   {
        try
        {
       _db.LocalGovernments.Add(new LocalGovernment
     {
      Id = Guid.NewGuid(),
  TenantId = _currentUser.IsGlobalAdmin ? null : _currentUser.TenantId,
       Name = row.Name ?? "",
       Locality = row.Locality,
              County = row.County,
          Address = row.Address,
        PostalCode = row.PostalCode,
    Phone = row.Phone,
     Fax = row.Fax,
      Email = row.Email,
              Website = row.Website,
   ContactPerson = row.ContactPerson,
                  ScheduleHours = row.ScheduleHours,
           Notes = row.Notes,
           CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
          });
             imported++;
      }
   catch (Exception ex) { errors.Add($"Row {imported + 1}: {ex.Message}"); }
            }

  await _db.SaveChangesAsync();
            await _audit.LogAsync("LocalGov.CsvImported", changes: new { imported, errors = errors.Count });
            return Ok(new { imported, errors });
        }
      catch (Exception ex)
 {
            return BadRequest(new { message = $"CSV parse error: {ex.Message}" });
        }
    }

    [HttpGet("export-csv")]
  [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> ExportCsv()
    {
     IQueryable<LocalGovernment> query = _db.LocalGovernments.AsNoTracking().IgnoreQueryFilters();
   if (!_currentUser.IsGlobalAdmin)
query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        var items = await query.OrderBy(t => t.County).ThenBy(t => t.Name).ToListAsync();

        using var writer = new StringWriter();
   using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(items.Select(t => new AuthorityCsvRow
        {
     Name = t.Name, Locality = t.Locality, County = t.County, Address = t.Address,
            PostalCode = t.PostalCode, Phone = t.Phone, Fax = t.Fax, Email = t.Email,
    Website = t.Website, ContactPerson = t.ContactPerson,
       ScheduleHours = t.ScheduleHours, Notes = t.Notes,
  }));

        var bytes = Encoding.UTF8.GetBytes(writer.ToString());
        await _audit.LogAsync("LocalGov.CsvExported", changes: new { count = items.Count });
        return File(bytes, "text/csv", $"local_governments_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

public record LocalGovernmentRequest(
    string Name, string? Locality, string? County, string? Address, string? PostalCode,
    string? Phone, string? Fax, string? Email, string? Website, string? ContactPerson,
    string? ScheduleHours, string? Notes, Guid? OverridesGlobalId);
