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
[Route("api/[controller]")]
[Authorize]
public class TribunalsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

  public TribunalsController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
   _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Get tribunals: global records + tenant overrides (if TenantAdmin).</summary>
    [HttpGet]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetAll()
  {
        IQueryable<Tribunal> query = _db.Set<Tribunal>().AsNoTracking();

 if (_currentUser.IsGlobalAdmin)
 {
       // GlobalAdmin sees ALL records (global + all tenant overrides)
     query = query.IgnoreQueryFilters();
        }
      else
        {
            // TenantAdmin sees: global records + their tenant's overrides
          query = query.IgnoreQueryFilters()
    .Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);
        }

        var tribunals = await query
        .OrderBy(t => t.County).ThenBy(t => t.Name)
.Select(t => new
            {
              t.Id,
                t.TenantId,
         t.Name,
        t.Section,
             t.Locality,
                t.County,
       t.Address,
    t.PostalCode,
           t.RegistryPhone,
    t.RegistryFax,
      t.RegistryEmail,
          t.RegistryHours,
 t.Website,
       t.ContactPerson,
       t.Notes,
           t.OverridesGlobalId,
             IsGlobal = t.TenantId == null,
             IsTenantOverride = t.TenantId != null,
       })
   .ToListAsync();

        return Ok(tribunals);
    }

    /// <summary>Get a single tribunal by ID.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> GetById(Guid id)
    {
 var tribunal = await _db.Set<Tribunal>().AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tribunal == null) return NotFound();

     // Security: TenantAdmins can only see global or their own tenant's records
        if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != null && tribunal.TenantId != _currentUser.TenantId)
            return Forbid();

        return Ok(tribunal);
    }

    /// <summary>Create or update a tribunal (GlobalAdmin: global record; TenantAdmin: tenant override).</summary>
    [HttpPost]
  [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Create([FromBody] TribunalRequest request)
    {
        var tribunal = new Tribunal
        {
        Id = Guid.NewGuid(),
    TenantId = _currentUser.IsGlobalAdmin ? null : _currentUser.TenantId, // Global or tenant-specific
            Name = request.Name,
Section = request.Section,
      Locality = request.Locality,
            County = request.County,
            Address = request.Address,
            PostalCode = request.PostalCode,
          RegistryPhone = request.RegistryPhone,
    RegistryFax = request.RegistryFax,
     RegistryEmail = request.RegistryEmail,
            RegistryHours = request.RegistryHours,
          Website = request.Website,
  ContactPerson = request.ContactPerson,
            Notes = request.Notes,
            OverridesGlobalId = request.OverridesGlobalId,
          CreatedOn = DateTime.UtcNow,
  CreatedBy = _currentUser.Email ?? "System",
        };

        _db.Set<Tribunal>().Add(tribunal);
        await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("Tribunal.Created", "Tribunal", tribunal.Id,
  newValues: new { tribunal.Name, tribunal.County, IsGlobal = tribunal.TenantId == null });

        return CreatedAtAction(nameof(GetById), new { id = tribunal.Id }, tribunal);
    }

    /// <summary>Update a tribunal.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TribunalRequest request)
    {
        var tribunal = await _db.Set<Tribunal>().IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tribunal == null) return NotFound();

  // Security: TenantAdmins can only edit their own tenant's overrides
        if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != _currentUser.TenantId)
            return Forbid();

      var oldValues = new { tribunal.Name, tribunal.County, tribunal.RegistryPhone, tribunal.RegistryEmail };

     tribunal.Name = request.Name;
tribunal.Section = request.Section;
    tribunal.Locality = request.Locality;
  tribunal.County = request.County;
   tribunal.Address = request.Address;
        tribunal.PostalCode = request.PostalCode;
        tribunal.RegistryPhone = request.RegistryPhone;
     tribunal.RegistryFax = request.RegistryFax;
        tribunal.RegistryEmail = request.RegistryEmail;
        tribunal.RegistryHours = request.RegistryHours;
  tribunal.Website = request.Website;
        tribunal.ContactPerson = request.ContactPerson;
        tribunal.Notes = request.Notes;
    tribunal.OverridesGlobalId = request.OverridesGlobalId;

     await _db.SaveChangesAsync();

        await _audit.LogEntityAsync("Tribunal.Updated", "Tribunal", tribunal.Id, oldValues,
   new { tribunal.Name, tribunal.County, tribunal.RegistryPhone, tribunal.RegistryEmail });

        return Ok(tribunal);
    }

    /// <summary>Delete a tribunal.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.SettingsEdit)]
    public async Task<IActionResult> Delete(Guid id)
    {
  var tribunal = await _db.Set<Tribunal>().IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tribunal == null) return NotFound();

        // Security
     if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != _currentUser.TenantId)
            return Forbid();

  _db.Set<Tribunal>().Remove(tribunal);
        await _db.SaveChangesAsync();

   await _audit.LogEntityAsync("Tribunal.Deleted", "Tribunal", tribunal.Id,
      oldValues: new { tribunal.Name, tribunal.County }, severity: "Warning");

        return NoContent();
    }

    /// <summary>Import tribunals from CSV (GlobalAdmin: global; TenantAdmin: tenant overrides).</summary>
    [HttpPost("import-csv")]
    [RequirePermission(Permission.SettingsEdit)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportCsv([FromForm] IFormFile file)
    {
      if (file == null || file.Length == 0)
         return BadRequest(new { message = "No file uploaded" });

        var imported = 0;
        var errors = new List<string>();

        try
        {
     using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
   using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

      var records = csv.GetRecords<TribunalCsvRow>().ToList();

         foreach (var row in records)
            {
  try
            {
       var tribunal = new Tribunal
  {
             Id = Guid.NewGuid(),
  TenantId = _currentUser.IsGlobalAdmin ? null : _currentUser.TenantId,
          Name = row.Name ?? "",
         Section = row.Section,
        Locality = row.Locality,
     County = row.County,
               Address = row.Address,
       PostalCode = row.PostalCode,
              RegistryPhone = row.RegistryPhone,
   RegistryFax = row.RegistryFax,
     RegistryEmail = row.RegistryEmail,
          RegistryHours = row.RegistryHours,
     Website = row.Website,
       ContactPerson = row.ContactPerson,
 Notes = row.Notes,
        CreatedOn = DateTime.UtcNow,
             CreatedBy = _currentUser.Email ?? "System",
                };

 _db.Set<Tribunal>().Add(tribunal);
            imported++;
                }
          catch (Exception ex)
             {
  errors.Add($"Row {imported + 1}: {ex.Message}");
   }
            }

      await _db.SaveChangesAsync();

       await _audit.LogAsync("Tribunal.CsvImported", changes: new { imported, errors = errors.Count });

        return Ok(new { imported, errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"CSV parse error: {ex.Message}" });
        }
    }

    /// <summary>Export tribunals to CSV.</summary>
    [HttpGet("export-csv")]
    [RequirePermission(Permission.SettingsView)]
    public async Task<IActionResult> ExportCsv()
    {
        IQueryable<Tribunal> query = _db.Set<Tribunal>().AsNoTracking();

        if (!_currentUser.IsGlobalAdmin)
    {
            query = query.IgnoreQueryFilters()
 .Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);
        }
        else
        {
  query = query.IgnoreQueryFilters();
        }

 var tribunals = await query.OrderBy(t => t.County).ThenBy(t => t.Name).ToListAsync();

   using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

  csv.WriteRecords(tribunals.Select(t => new TribunalCsvRow
        {
            Name = t.Name,
    Section = t.Section,
            Locality = t.Locality,
    County = t.County,
            Address = t.Address,
         PostalCode = t.PostalCode,
            RegistryPhone = t.RegistryPhone,
   RegistryFax = t.RegistryFax,
    RegistryEmail = t.RegistryEmail,
          RegistryHours = t.RegistryHours,
     Website = t.Website,
            ContactPerson = t.ContactPerson,
 Notes = t.Notes,
        }));

     var csvContent = writer.ToString();
        var bytes = Encoding.UTF8.GetBytes(csvContent);

        await _audit.LogAsync("Tribunal.CsvExported", changes: new { count = tribunals.Count });

        return File(bytes, "text/csv", $"tribunals_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

public record TribunalRequest(
    string Name,
    string? Section,
    string? Locality,
    string? County,
  string? Address,
    string? PostalCode,
    string? RegistryPhone,
    string? RegistryFax,
    string? RegistryEmail,
    string? RegistryHours,
    string? Website,
    string? ContactPerson,
    string? Notes,
    Guid? OverridesGlobalId
);

public class TribunalCsvRow
{
  public string? Name { get; set; }
    public string? Section { get; set; }
    public string? Locality { get; set; }
    public string? County { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? RegistryPhone { get; set; }
  public string? RegistryFax { get; set; }
    public string? RegistryEmail { get; set; }
    public string? RegistryHours { get; set; }
    public string? Website { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
}
