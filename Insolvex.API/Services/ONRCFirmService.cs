using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Implements ONRC firm database operations: CSV import, search by CUI/Name.
/// Data is system-wide (not tenant-scoped) but filtered by region.
/// </summary>
public sealed class ONRCFirmService : IONRCFirmService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<ONRCFirmService> _logger;

    public ONRCFirmService(ApplicationDbContext db, IAuditService audit, ILogger<ONRCFirmService> logger)
    {
     _db = db;
 _audit = audit;
        _logger = logger;
    }

    public async Task<List<ONRCFirmResult>> SearchByCuiAsync(string cui, SystemRegion region, int maxResults, CancellationToken ct)
    {
        var normalized = cui.Trim().TrimStart('R', 'O', 'r', 'o');
        return await _db.ONRCFirmRecords
        .IgnoreQueryFilters()
       .Where(f => f.Region == region && f.CUI.StartsWith(normalized))
          .OrderBy(f => f.CUI)
.Take(maxResults)
        .Select(f => MapToResult(f))
            .ToListAsync(ct);
    }

    public async Task<List<ONRCFirmResult>> SearchByNameAsync(string name, SystemRegion region, int maxResults, CancellationToken ct)
    {
        var term = name.Trim().ToUpperInvariant();
        return await _db.ONRCFirmRecords
 .IgnoreQueryFilters()
            .Where(f => f.Region == region && f.Name.ToUpper().Contains(term))
            .OrderBy(f => f.Name)
 .Take(maxResults)
      .Select(f => MapToResult(f))
   .ToListAsync(ct);
    }

    public async Task<List<ONRCFirmResult>> SearchAsync(string query, SystemRegion region, int maxResults, CancellationToken ct)
    {
        var trimmed = query.Trim();
        // If the query is numeric-ish (e.g. "12345678" or "RO12345678"), search by CUI
        var numericPart = trimmed.TrimStart('R', 'O', 'r', 'o');
        if (numericPart.Length > 0 && numericPart.All(char.IsDigit))
            return await SearchByCuiAsync(trimmed, region, maxResults, ct);

        return await SearchByNameAsync(trimmed, region, maxResults, ct);
    }

    public async Task<ONRCImportResult> ImportFromCsvAsync(Stream csvStream, SystemRegion region, CancellationToken ct)
    {
        var result = new ONRCImportResult();
        var errors = new List<string>();
        var records = new List<ONRCFirmRecord>();
     var lineNumber = 0;

        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
  {
    errors.Add("CSV file is empty or missing header row.");
       return new ONRCImportResult { Errors = errors };
        }

    // Parse header to find column indices
     var headers = ParseCsvLine(headerLine);
        var colMap = BuildColumnMap(headers);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
      lineNumber++;
         if (string.IsNullOrWhiteSpace(line)) continue;

            try
      {
       var fields = ParseCsvLine(line);
        var cui = GetField(fields, colMap, "cui")?.Trim().TrimStart('R', 'O', 'r', 'o');
    var name = GetField(fields, colMap, "name")?.Trim();

          if (string.IsNullOrWhiteSpace(cui) || string.IsNullOrWhiteSpace(name))
                {
           errors.Add($"Row {lineNumber}: Missing CUI or Name, skipped.");
 result = result with { Skipped = result.Skipped + 1 };
 continue;
             }

    records.Add(new ONRCFirmRecord
      {
    Id = Guid.NewGuid(),
        Region = region,
        CUI = cui,
          Name = name,
     TradeRegisterNo = GetField(fields, colMap, "traderegisterno"),
        CAEN = GetField(fields, colMap, "caen"),
           Address = GetField(fields, colMap, "address"),
          Locality = GetField(fields, colMap, "locality"),
         County = GetField(fields, colMap, "county"),
    PostalCode = GetField(fields, colMap, "postalcode"),
           Phone = GetField(fields, colMap, "phone"),
        Status = GetField(fields, colMap, "status"),
          IncorporationYear = GetField(fields, colMap, "incorporationyear"),
 ShareCapitalRon = ParseDecimal(GetField(fields, colMap, "sharecapitalron")),
     ImportedAt = DateTime.UtcNow,
      CreatedOn = DateTime.UtcNow,
       CreatedBy = "ONRCImport",
        });
         }
            catch (Exception ex)
            {
         errors.Add($"Row {lineNumber}: {ex.Message}");
    }
   }

        result = result with { TotalRows = lineNumber };

  if (records.Count == 0)
        {
     errors.Add("No valid records found in CSV.");
  return new ONRCImportResult { TotalRows = lineNumber, Errors = errors };
        }

// Upsert: match on CUI + Region
        var existingCuis = await _db.ONRCFirmRecords
  .IgnoreQueryFilters()
        .Where(f => f.Region == region)
          .Select(f => f.CUI)
       .ToHashSetAsync(ct);

        var toInsert = new List<ONRCFirmRecord>();
        var toUpdate = 0;

      foreach (var rec in records)
     {
            if (existingCuis.Contains(rec.CUI))
            {
    // Update existing
      var existing = await _db.ONRCFirmRecords
    .IgnoreQueryFilters()
.FirstAsync(f => f.Region == region && f.CUI == rec.CUI, ct);

existing.Name = rec.Name;
              existing.TradeRegisterNo = rec.TradeRegisterNo;
  existing.CAEN = rec.CAEN;
       existing.Address = rec.Address;
 existing.Locality = rec.Locality;
    existing.County = rec.County;
           existing.PostalCode = rec.PostalCode;
      existing.Phone = rec.Phone;
        existing.Status = rec.Status;
            existing.IncorporationYear = rec.IncorporationYear;
 existing.ShareCapitalRon = rec.ShareCapitalRon;
             existing.ImportedAt = DateTime.UtcNow;
      toUpdate++;
      }
            else
        {
 toInsert.Add(rec);
   existingCuis.Add(rec.CUI);
            }
}

        if (toInsert.Count > 0)
         _db.ONRCFirmRecords.AddRange(toInsert);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
      Action = "ONRCFirmDatabase.Imported",
     Description = $"ONRC firm database for {region} updated: {toInsert.Count} new records imported, {toUpdate} updated from CSV ({lineNumber} total rows).",
 EntityType = "ONRCFirmDatabase",
        NewValues = new { region = region.ToString(), imported = toInsert.Count, updated = toUpdate, totalRows = lineNumber, errors = errors.Count },
            Severity = "Info",
            Category = "SystemData",
        });

        _logger.LogInformation("ONRC import for {Region}: {Imported} new, {Updated} updated, {Errors} errors from {TotalRows} rows",
 region, toInsert.Count, toUpdate, errors.Count, lineNumber);

        return new ONRCImportResult
        {
       TotalRows = lineNumber,
      Imported = toInsert.Count,
          Updated = toUpdate,
            Skipped = result.Skipped,
    Errors = errors,
        };
    }

  public async Task<ONRCDatabaseStats> GetStatsAsync(SystemRegion region, CancellationToken ct)
    {
        var count = await _db.ONRCFirmRecords
            .IgnoreQueryFilters()
     .CountAsync(f => f.Region == region, ct);

 var lastImport = await _db.ONRCFirmRecords
            .IgnoreQueryFilters()
            .Where(f => f.Region == region)
         .OrderByDescending(f => f.ImportedAt)
      .Select(f => (DateTime?)f.ImportedAt)
            .FirstOrDefaultAsync(ct);

        return new ONRCDatabaseStats
        {
Region = region.ToString(),
        TotalRecords = count,
    LastImportedAt = lastImport,
        };
    }

    // ?? Helpers ??????????????????????????????????????????????

    private static ONRCFirmResult MapToResult(ONRCFirmRecord f) => new()
  {
        Id = f.Id,
     CUI = f.CUI,
     Name = f.Name,
      TradeRegisterNo = f.TradeRegisterNo,
        CAEN = f.CAEN,
        Address = f.Address,
        Locality = f.Locality,
        County = f.County,
        PostalCode = f.PostalCode,
        Phone = f.Phone,
  Status = f.Status,
        IncorporationYear = f.IncorporationYear,
        ShareCapitalRon = f.ShareCapitalRon,
   Region = f.Region.ToString(),
    };

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["cui"] = ["cui", "cif", "cod_fiscal", "fiscal_code", "cod fiscal", "CUI"],
         ["name"] = ["name", "denumire", "firma", "company_name", "company name", "Denumire"],
   ["traderegisterno"] = ["trade_register_no", "nr_reg_com", "reg_com", "j_number", "Nr. Reg. Com."],
       ["caen"] = ["caen", "cod_caen", "activity_code", "CAEN"],
            ["address"] = ["address", "adresa", "sediu", "Adresa"],
            ["locality"] = ["locality", "localitate", "oras", "city", "Localitate"],
    ["county"] = ["county", "judet", "Judet"],
 ["postalcode"] = ["postal_code", "cod_postal", "zip", "Cod Postal"],
      ["phone"] = ["phone", "telefon", "tel", "Telefon"],
    ["status"] = ["status", "stare", "stare_firma", "Stare"],
          ["incorporationyear"] = ["incorporation_year", "an_infiintare", "year", "An Infiintare"],
            ["sharecapitalron"] = ["share_capital", "capital_social", "capital", "Capital Social"],
    };

     for (int i = 0; i < headers.Length; i++)
   {
          var h = headers[i].Trim().Trim('"');
   foreach (var (key, synonyms) in aliases)
            {
         if (synonyms.Any(s => s.Equals(h, StringComparison.OrdinalIgnoreCase)))
         {
        map.TryAdd(key, i);
      break;
      }
 }
        }

    return map;
    }

    private static string? GetField(string[] fields, Dictionary<string, int> colMap, string key)
    {
        if (!colMap.TryGetValue(key, out var idx) || idx >= fields.Length) return null;
        var val = fields[idx].Trim().Trim('"');
    return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private static string[] ParseCsvLine(string line)
    {
 // Simple CSV parser handling quoted fields
        var fields = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
   var c = line[i];
            if (c == '"')
        {
   if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
          {
      current += '"';
       i++;
          }
             else
{
           inQuotes = !inQuotes;
  }
        }
            else if (c == ',' && !inQuotes)
       {
     fields.Add(current);
       current = "";
            }
   else
{
       current += c;
       }
        }
    fields.Add(current);
      return fields.ToArray();
    }

    private static decimal? ParseDecimal(string? value)
    {
 if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

// Extension method for ToHashSetAsync
internal static class QueryableExtensions
{
    public static async Task<HashSet<T>> ToHashSetAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
     var set = new HashSet<T>();
        await foreach (var item in source.AsAsyncEnumerable().WithCancellation(ct))
            set.Add(item);
        return set;
    }
}
