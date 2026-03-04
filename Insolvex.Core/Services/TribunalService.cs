using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Services;

/// <summary>
/// Manages court / tribunal reference data.
/// GlobalAdmins own global records; TenantAdmins manage tenant-level overrides.
/// </summary>
public sealed class TribunalService : ITribunalService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public TribunalService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<TribunalDto>> GetAllAsync(CancellationToken ct = default)
    {
        var query = _db.Set<Tribunal>().AsNoTracking().IgnoreQueryFilters();

        if (!_currentUser.IsGlobalAdmin)
            query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        return await query
            .OrderBy(t => t.County).ThenBy(t => t.Name)
            .Select(t => new TribunalDto(
                t.Id, t.TenantId, t.Name, t.Section, t.Locality, t.County,
                t.Address, t.PostalCode, t.RegistryPhone, t.RegistryFax,
                t.RegistryEmail, t.RegistryHours, t.Website, t.ContactPerson,
                t.Notes, t.OverridesGlobalId,
                t.TenantId == null,
                t.TenantId != null))
            .ToListAsync(ct);
    }

    public async Task<TribunalDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tribunal = await _db.Set<Tribunal>().AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tribunal is null) return null;

        if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != null && tribunal.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You do not have access to this court record.");

        return ToDto(tribunal);
    }

    public async Task<TribunalDto> CreateAsync(TribunalRequest request, CancellationToken ct = default)
    {
        var tribunal = new Tribunal
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.IsGlobalAdmin ? null : _currentUser.TenantId,
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
        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Court Record Created", "Tribunal", tribunal.Id,
            newValues: new { tribunal.Name, tribunal.County, IsGlobal = tribunal.TenantId == null });

        return ToDto(tribunal);
    }

    public async Task<TribunalDto> UpdateAsync(Guid id, TribunalRequest request, CancellationToken ct = default)
    {
        var tribunal = await _db.Set<Tribunal>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Court record", id);

        if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You can only edit your own tenant's court records.");

        var old = new { tribunal.Name, tribunal.County, tribunal.RegistryPhone, tribunal.RegistryEmail };

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
        tribunal.LastModifiedOn = DateTime.UtcNow;
        tribunal.LastModifiedBy = _currentUser.Email;

        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Court Record Updated", "Tribunal", tribunal.Id, old,
            new { tribunal.Name, tribunal.County, tribunal.RegistryPhone, tribunal.RegistryEmail });

        return ToDto(tribunal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tribunal = await _db.Set<Tribunal>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Court record", id);

        if (!_currentUser.IsGlobalAdmin && tribunal.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You can only delete your own tenant's court records.");

        _db.Set<Tribunal>().Remove(tribunal);
        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Court Record Deleted", "Tribunal", tribunal.Id,
            oldValues: new { tribunal.Name, tribunal.County }, severity: "Warning");
    }

    public async Task<AuthorityImportResult> ImportCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        var imported = 0;
        var errors = new List<string>();

        using var reader = new StreamReader(csvStream, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

        var records = csv.GetRecords<TribunalCsvRow>().ToList();

        foreach (var row in records)
        {
            try
            {
                _db.Set<Tribunal>().Add(new Tribunal
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
                });
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {imported + errors.Count + 1}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            "Court Records Imported from CSV",
            changes: new { imported, errors = errors.Count });

        return new AuthorityImportResult(imported, errors.Count, errors);
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var query = _db.Set<Tribunal>().AsNoTracking().IgnoreQueryFilters();

        if (!_currentUser.IsGlobalAdmin)
            query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        var tribunals = await query.OrderBy(t => t.County).ThenBy(t => t.Name).ToListAsync(ct);

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

        await _audit.LogAsync(
            "Court Records Exported to CSV",
            changes: new { count = tribunals.Count });

        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TribunalDto ToDto(Tribunal t) => new(
        t.Id, t.TenantId, t.Name, t.Section, t.Locality, t.County,
        t.Address, t.PostalCode, t.RegistryPhone, t.RegistryFax,
        t.RegistryEmail, t.RegistryHours, t.Website, t.ContactPerson,
        t.Notes, t.OverridesGlobalId,
        IsGlobal: t.TenantId == null,
        IsTenantOverride: t.TenantId != null);
}
