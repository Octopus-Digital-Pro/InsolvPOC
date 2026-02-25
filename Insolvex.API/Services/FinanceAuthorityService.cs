using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

/// <summary>
/// Manages ANAF finance authority reference data.
/// GlobalAdmins own global records; TenantAdmins manage tenant-level overrides.
/// </summary>
public sealed class FinanceAuthorityService : IFinanceAuthorityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public FinanceAuthorityService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<AuthorityDto>> GetAllAsync(CancellationToken ct = default)
    {
        var query = _db.FinanceAuthorities.AsNoTracking().IgnoreQueryFilters();

        if (!_currentUser.IsGlobalAdmin)
            query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        return await query
            .OrderBy(t => t.County).ThenBy(t => t.Name)
            .Select(t => new AuthorityDto(
                t.Id, t.TenantId, t.Name, t.Locality, t.County,
                t.Address, t.PostalCode, t.Phone, t.Fax, t.Email,
                t.Website, t.ContactPerson, t.ScheduleHours, t.Notes,
                t.OverridesGlobalId,
                t.TenantId == null,
                t.TenantId != null))
            .ToListAsync(ct);
    }

    public async Task<AuthorityDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.FinanceAuthorities.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (item is null) return null;

        if (!_currentUser.IsGlobalAdmin && item.TenantId != null && item.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You do not have access to this finance authority record.");

        return ToDto(item);
    }

    public async Task<AuthorityDto> CreateAsync(FinanceAuthorityRequest request, CancellationToken ct = default)
    {
        var item = new FinanceAuthority
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

        _db.FinanceAuthorities.Add(item);
        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Finance Authority Created", "FinanceAuthority", item.Id,
            newValues: new { item.Name, item.County, IsGlobal = item.TenantId == null });

        return ToDto(item);
    }

    public async Task<AuthorityDto> UpdateAsync(Guid id, FinanceAuthorityRequest request, CancellationToken ct = default)
    {
        var item = await _db.FinanceAuthorities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Finance authority", id);

        if (!_currentUser.IsGlobalAdmin && item.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You can only edit your own tenant's finance authority records.");

        var old = new { item.Name, item.County, item.Phone, item.Email };

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
        item.LastModifiedOn = DateTime.UtcNow;
        item.LastModifiedBy = _currentUser.Email;

        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Finance Authority Updated", "FinanceAuthority", item.Id, old,
            new { item.Name, item.County, item.Phone, item.Email });

        return ToDto(item);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.FinanceAuthorities.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Finance authority", id);

        if (!_currentUser.IsGlobalAdmin && item.TenantId != _currentUser.TenantId)
            throw new ForbiddenException("You can only delete your own tenant's finance authority records.");

        _db.FinanceAuthorities.Remove(item);
        await _db.SaveChangesAsync(ct);

        await _audit.LogEntityAsync(
            "Finance Authority Deleted", "FinanceAuthority", item.Id,
            oldValues: new { item.Name, item.County }, severity: "Warning");
    }

    public async Task<AuthorityImportResult> ImportCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        var imported = 0;
        var errors = new List<string>();

        using var reader = new StreamReader(csvStream, Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

        var records = csv.GetRecords<AuthorityCsvRow>().ToList();

        foreach (var row in records)
        {
            try
            {
                _db.FinanceAuthorities.Add(new FinanceAuthority
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
            catch (Exception ex)
            {
                errors.Add($"Row {imported + errors.Count + 1}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            "Finance Authorities Imported from CSV",
            changes: new { imported, errors = errors.Count });

        return new AuthorityImportResult(imported, errors.Count, errors);
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var query = _db.FinanceAuthorities.AsNoTracking().IgnoreQueryFilters();

        if (!_currentUser.IsGlobalAdmin)
            query = query.Where(t => t.TenantId == null || t.TenantId == _currentUser.TenantId);

        var items = await query.OrderBy(t => t.County).ThenBy(t => t.Name).ToListAsync(ct);

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteRecords(items.Select(t => new AuthorityCsvRow
        {
            Name = t.Name,
            Locality = t.Locality,
            County = t.County,
            Address = t.Address,
            PostalCode = t.PostalCode,
            Phone = t.Phone,
            Fax = t.Fax,
            Email = t.Email,
            Website = t.Website,
            ContactPerson = t.ContactPerson,
            ScheduleHours = t.ScheduleHours,
            Notes = t.Notes,
        }));

        await _audit.LogAsync(
            "Finance Authorities Exported to CSV",
            changes: new { count = items.Count });

        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AuthorityDto ToDto(FinanceAuthority t) => new(
        t.Id, t.TenantId, t.Name, t.Locality, t.County,
        t.Address, t.PostalCode, t.Phone, t.Fax, t.Email,
        t.Website, t.ContactPerson, t.ScheduleHours, t.Notes,
        t.OverridesGlobalId,
        IsGlobal: t.TenantId == null,
        IsTenantOverride: t.TenantId != null);
}
