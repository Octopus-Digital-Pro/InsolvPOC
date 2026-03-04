using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Services;

/// <summary>
/// Manages ANAF finance authority reference data.
/// GlobalAdmins own global records; TenantAdmins manage tenant-level overrides.
/// </summary>
public sealed class FinanceAuthorityService : IFinanceAuthorityService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IHttpClientFactory _http;

    public FinanceAuthorityService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit, IHttpClientFactory http)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _http = http;
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
                t.TenantId != null,
                t.ParentId,
                t.Parent != null ? t.Parent.Name : null))
            .ToListAsync(ct);
    }

    public async Task<AuthorityDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.FinanceAuthorities.AsNoTracking().IgnoreQueryFilters()
            .Include(t => t.Parent)
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
            ParentId = request.ParentId,
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
        item.ParentId = request.ParentId;
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

    // ── ANAF Scraper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the ANAF regional offices page, parses each county-level
    /// Administrația Județeană accordion and its sub-office table rows, then
    /// upserts global FinanceAuthority records in a parent → child hierarchy.
    /// </summary>
    public async Task<AnafScrapeResult> ScrapeAnafAsync(string url, CancellationToken ct = default)
    {
        var created = 0;
        var updated = 0;
        var errors  = new List<string>();

        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "InsolvexBot/1.0");
            var html = await client.GetStringAsync(url, ct);

            var sections = ParseAnafSubOffices(html);

            foreach (var section in sections)
            {
                try
                {
                    // ── Upsert the parent record (Administrația Județeană) ──
                    var parentNameLower = section.AdminName.ToLowerInvariant().Trim();
                    var parentEntity = await _db.FinanceAuthorities
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(f =>
                            f.TenantId == null && f.ParentId == null &&
                            f.Name.ToLower() == parentNameLower, ct);

                    // Use the first table row (usually the county admin itself) for contact info
                    var adminRow = section.Offices.FirstOrDefault();

                    if (parentEntity != null)
                    {
                        if (adminRow != null)
                        {
                            parentEntity.County         = section.County  ?? parentEntity.County;
                            parentEntity.Locality       = adminRow.Locality ?? parentEntity.Locality;
                            parentEntity.Address        = adminRow.Address ?? parentEntity.Address;
                            parentEntity.PostalCode     = adminRow.PostalCode ?? parentEntity.PostalCode;
                            parentEntity.Phone          = adminRow.Phone ?? parentEntity.Phone;
                            parentEntity.Fax            = adminRow.Fax ?? parentEntity.Fax;
                        }
                        parentEntity.Website        = "https://www.anaf.ro";
                        parentEntity.LastModifiedOn = DateTime.UtcNow;
                        parentEntity.LastModifiedBy = _currentUser.Email ?? "System";
                        updated++;
                    }
                    else
                    {
                        parentEntity = new FinanceAuthority
                        {
                            Id          = Guid.NewGuid(),
                            TenantId    = null,
                            ParentId    = null,
                            Name        = section.AdminName,
                            County      = section.County,
                            Locality    = adminRow?.Locality,
                            Address     = adminRow?.Address,
                            PostalCode  = adminRow?.PostalCode,
                            Phone       = adminRow?.Phone,
                            Fax         = adminRow?.Fax,
                            Website     = "https://www.anaf.ro",
                            CreatedOn   = DateTime.UtcNow,
                            CreatedBy   = _currentUser.Email ?? "System",
                        };
                        _db.FinanceAuthorities.Add(parentEntity);
                        created++;
                    }

                    // Save parent first so its Id is available for children
                    await _db.SaveChangesAsync(ct);

                    // ── Upsert child offices (Unități Fiscale) ──
                    // Skip the first row if it matches the parent name (the admin itself)
                    var childOffices = section.Offices;
                    if (childOffices.Count > 0 &&
                        childOffices[0].Name.Equals(section.AdminName, StringComparison.OrdinalIgnoreCase))
                    {
                        childOffices = childOffices.Skip(1).ToList();
                    }

                    foreach (var office in childOffices)
                    {
                        try
                        {
                            var officeLower = office.Name.ToLowerInvariant().Trim();
                            var existing = await _db.FinanceAuthorities
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(f =>
                                    f.TenantId == null &&
                                    f.Name.ToLower() == officeLower, ct);

                            if (existing != null)
                            {
                                existing.ParentId       = parentEntity.Id;
                                existing.County         = office.County ?? existing.County;
                                existing.Locality       = office.Locality ?? existing.Locality;
                                existing.Address        = office.Address ?? existing.Address;
                                existing.PostalCode     = office.PostalCode ?? existing.PostalCode;
                                existing.Phone          = office.Phone ?? existing.Phone;
                                existing.Fax            = office.Fax ?? existing.Fax;
                                existing.Website        = "https://www.anaf.ro";
                                existing.LastModifiedOn = DateTime.UtcNow;
                                existing.LastModifiedBy = _currentUser.Email ?? "System";
                                updated++;
                            }
                            else
                            {
                                _db.FinanceAuthorities.Add(new FinanceAuthority
                                {
                                    Id          = Guid.NewGuid(),
                                    TenantId    = null,
                                    ParentId    = parentEntity.Id,
                                    Name        = office.Name,
                                    County      = office.County,
                                    Locality    = office.Locality,
                                    Address     = office.Address,
                                    PostalCode  = office.PostalCode,
                                    Phone       = office.Phone,
                                    Fax         = office.Fax,
                                    Website     = "https://www.anaf.ro",
                                    CreatedOn   = DateTime.UtcNow,
                                    CreatedBy   = _currentUser.Email ?? "System",
                                });
                                created++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{office.Name}: {ex.Message}");
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    errors.Add($"{section.AdminName}: {ex.Message}");
                }
            }

            await _audit.LogAsync(
                "ANAF Offices Scraped",
                changes: new { url, created, updated, errorCount = errors.Count });
        }
        catch (Exception ex)
        {
            errors.Add($"Scrape failed: {ex.Message}");
        }

        return new AnafScrapeResult(created, updated, errors.Count, errors);
    }

    // ── Private: ANAF HTML parser ─────────────────────────────────────────────

    private sealed record AnafOfficeEntry(
        string Name,
        string? County,
        string? Locality,
        string? Address,
        string? PostalCode,
        string? Phone,
        string? Fax);

    private sealed record AnafCountySection(
        string AdminName,
        string? County,
        List<AnafOfficeEntry> Offices);

    /// <summary>
    /// Parses the ANAF Regiuni.htm page which contains accordion sections
    /// for each Administrația Județeană, each with a table of sub-offices.
    /// </summary>
    private static List<AnafCountySection> ParseAnafSubOffices(string html)
    {
        var results = new List<AnafCountySection>();

        // Strip script / style blocks
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Match each accordion button (county admin) + its panel table
        var sectionPattern = new Regex(
            @"<button\s+class=""accordion1[^""]*""[^>]*>\s*<p\s+class=""stilCapitol"">(.*?)</p>\s*</button>\s*<div\s+class=""panel""[^>]*>\s*<table[^>]*>(.*?)</table>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // The page duplicates content for each DGRFP region — deduplicate
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match section in sectionPattern.Matches(html))
        {
            var adminName = NormaliseWhitespace(StripTags(section.Groups[1].Value));
            if (!seen.Add(adminName)) continue;

            var tableHtml = section.Groups[2].Value;

            // Extract county from name: "Administrația Județeană a Finanțelor Publice ALBA" → "ALBA"
            var countyMatch = Regex.Match(adminName,
                @"(?:Finan[țt]elor|Finantelor)\s+Publice\s+(.+)$", RegexOptions.IgnoreCase);
            var county = countyMatch.Success ? countyMatch.Groups[1].Value.Trim() : null;

            var offices = new List<AnafOfficeEntry>();

            // Parse data rows (skip header rows that contain <th>)
            var rowPattern = new Regex(
                @"<tr>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*</tr>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match row in rowPattern.Matches(tableHtml))
            {
                var cellHtml = row.Groups[1].Value;
                if (cellHtml.Contains("<th", StringComparison.OrdinalIgnoreCase)) continue;

                var office = ParseOfficeCell(cellHtml, county);
                if (office != null)
                    offices.Add(office);
            }

            if (offices.Count > 0)
                results.Add(new AnafCountySection(adminName, county, offices));
        }

        return results;
    }

    /// <summary>Parses a single table cell containing office name, address, and phone.</summary>
    private static AnafOfficeEntry? ParseOfficeCell(string cellHtml, string? county)
    {
        // Extract name from <strong>
        var nameMatch = Regex.Match(cellHtml, @"<strong>(.*?)</strong>", RegexOptions.Singleline);
        if (!nameMatch.Success) return null;
        var name = NormaliseWhitespace(StripTags(nameMatch.Groups[1].Value));

        // Only keep text before <hr> (the assistance section comes after)
        var hrIdx = cellHtml.IndexOf("<hr", StringComparison.OrdinalIgnoreCase);
        if (hrIdx > 0) cellHtml = cellHtml[..hrIdx];

        // Get the text after </strong>
        var afterStrong = cellHtml[(nameMatch.Index + nameMatch.Length)..];
        var text = NormaliseWhitespace(StripTags(afterStrong));

        // Split on "Tel." to separate address from phone
        var telIdx = text.IndexOf("Tel.", StringComparison.OrdinalIgnoreCase);
        var faxIdx = text.IndexOf("Fax", StringComparison.OrdinalIgnoreCase);
        var cutoff = telIdx >= 0 ? telIdx : (faxIdx >= 0 ? faxIdx : text.Length);
        var address = text[..cutoff].Trim().TrimEnd(',', ' ');

        // Extract postal code: "C.P. 510110" or "CP 510110"
        var cpMatch = Regex.Match(address, @"C\.?P\.?\s*(\d+)");
        var postalCode = cpMatch.Success ? cpMatch.Groups[1].Value : null;

        // ── Extract locality ──
        string? locality = null;

        // Pattern 1: City at the start before "Str." / "Piața" / "Calea" / "B-dul"
        var cityStart = Regex.Match(address,
            @"^([A-ZĂÂÎȘȚ\u00C0-\u024F][a-zăâîșțA-ZĂÂÎȘȚ\u00C0-\u024F\s\-]+?)\s*,\s*(?:Str\.|Pia[țt]a|Calea|B-dul|Bd\.)",
            RegexOptions.None);
        if (cityStart.Success)
        {
            locality = cityStart.Groups[1].Value.Trim();
        }
        else
        {
            // Pattern 2: City between "nr. XX," and "C.P."
            var cityMid = Regex.Match(address,
                @"nr\.?\s*[\d\w\-\s]+?,\s*([A-ZĂÂÎȘȚ\u00C0-\u024F][a-zăâîșțA-ZĂÂÎȘȚ\u00C0-\u024F\s\-]+?)\s*,\s*C\.?P\.?",
                RegexOptions.None);
            if (cityMid.Success)
            {
                var candidate = cityMid.Groups[1].Value.Trim();
                if (!candidate.StartsWith("sector", StringComparison.OrdinalIgnoreCase))
                    locality = candidate;
            }
        }

        // Fallback: extract city from office name
        if (string.IsNullOrWhiteSpace(locality))
        {
            // "Unitatea Fiscală Municipală Făgăraș" → "Făgăraș"
            // "Unitatea Fiscală Orășenească Câmpeni" → "Câmpeni"
            var nameLoc = Regex.Match(name,
                @"(?:Municipal[ăa]|Or[ăa][șs]eneasc[ăa])\s+(.+)$",
                RegexOptions.IgnoreCase);
            if (nameLoc.Success)
                locality = nameLoc.Groups[1].Value.Trim();
        }

        // Extract phone after "Tel."
        string? phone = null;
        if (telIdx >= 0)
        {
            var afterTel = text[(telIdx + 4)..];
            var phoneMatch = Regex.Match(afterTel, @"([\d\.\s,;/]+?)(?:Asisten|Fax|$)");
            phone = phoneMatch.Success ? phoneMatch.Groups[1].Value.Trim().TrimEnd(',', ';', ' ') : null;
        }

        // Extract fax after "Fax." or "Fax "
        string? fax = null;
        if (faxIdx >= 0)
        {
            var afterFax = text[(faxIdx + 3)..].TrimStart('.', ' ');
            var faxMatch = Regex.Match(afterFax, @"([\d\.\s,;/]+?)(?:Asisten|Tel|\)|$)");
            fax = faxMatch.Success ? faxMatch.Groups[1].Value.Trim().TrimEnd(',', ';', ' ') : null;
        }

        return new AnafOfficeEntry(name, county, locality, address, postalCode, phone, fax);
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", " ");

    private static string NormaliseWhitespace(string s) =>
        Regex.Replace(s.Replace("\r", " ").Replace("\n", " "), @"\s{2,}", " ").Trim();

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AuthorityDto ToDto(FinanceAuthority t) => new(
        t.Id, t.TenantId, t.Name, t.Locality, t.County,
        t.Address, t.PostalCode, t.Phone, t.Fax, t.Email,
        t.Website, t.ContactPerson, t.ScheduleHours, t.Notes,
        t.OverridesGlobalId,
        IsGlobal: t.TenantId == null,
        IsTenantOverride: t.TenantId != null,
        ParentId: t.ParentId,
        ParentName: t.Parent?.Name);
}
