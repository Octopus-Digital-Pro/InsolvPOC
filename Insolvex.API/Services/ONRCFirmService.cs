using System.Globalization;
using System.Text;
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
    const int BatchSize = 2_000;
    var errors = new List<string>(100);
    var totalRows = 0;
    var totalImported = 0;
    var totalUpdated = 0;
    var totalSkipped = 0;
    // Dictionary keyed by CUI deduplicates rows within the same batch
    var batch = new Dictionary<string, ONRCFirmRecord>(BatchSize, StringComparer.OrdinalIgnoreCase);

    using var reader = new StreamReader(csvStream, System.Text.Encoding.UTF8,
        detectEncodingFromByteOrderMarks: true, bufferSize: 65_536, leaveOpen: true);
    var headerLine = await reader.ReadLineAsync(ct);
    if (string.IsNullOrWhiteSpace(headerLine))
    {
      errors.Add("CSV file is empty or missing header row.");
      return new ONRCImportResult { Errors = errors };
    }

    // Auto-detect delimiter: ONRC official exports use '^'; standard CSVs use ','
    var caretCount = headerLine.Count(c => c == '^');
    var commaCount = headerLine.Count(c => c == ',');
    var delimiter = caretCount > commaCount ? '^' : ',';

    var headers = ParseCsvLine(headerLine, delimiter);
    var colMap = BuildColumnMap(headers);

    // Flush one batch: single WHERE-IN SELECT + batch INSERT/UPDATE + ChangeTracker.Clear()
    async Task FlushBatchAsync()
    {
      if (batch.Count == 0) return;
      var batchCuis = batch.Keys.ToList();
      // One query to find all existing records for this batch
      var existingMap = await _db.ONRCFirmRecords
          .IgnoreQueryFilters()
          .Where(f => f.Region == region && batchCuis.Contains(f.CUI))
          .ToDictionaryAsync(f => f.CUI, ct);

      foreach (var (cui, rec) in batch)
      {
        if (existingMap.TryGetValue(cui, out var existing))
        {
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
          totalUpdated++;
        }
        else
        {
          _db.ONRCFirmRecords.Add(rec);
          totalImported++;
        }
      }

      await _db.SaveChangesAsync(ct);
      _db.ChangeTracker.Clear(); // Release tracked entities — prevents memory growth over millions of rows
      batch.Clear();
    }

    while (!reader.EndOfStream)
    {
      ct.ThrowIfCancellationRequested();
      var line = await reader.ReadLineAsync(ct);
      if (line is null) break;
      totalRows++;
      if (string.IsNullOrWhiteSpace(line)) continue;

      try
      {
        var fields = ParseCsvLine(line, delimiter);
        var cui = GetField(fields, colMap, "cui")?.Trim().TrimStart('R', 'O', 'r', 'o');
        var name = GetField(fields, colMap, "name")?.Trim();

        if (string.IsNullOrWhiteSpace(cui) || string.IsNullOrWhiteSpace(name))
        {
          if (errors.Count < 100)
            errors.Add($"Row {totalRows}: Missing CUI or Name, skipped.");
          totalSkipped++;
          continue;
        }

        // Assemble address from ONRC split-column format (ADR_DEN_STRADA, ADR_NR_STRADA, etc.)
        var streetName = GetField(fields, colMap, "adr_den_strada");
        var streetNo   = GetField(fields, colMap, "adr_nr_strada");
        var address = streetName != null || streetNo != null
            ? AssembleRoAddress(
                streetName,
                streetNo,
                GetField(fields, colMap, "adr_bloc"),
                GetField(fields, colMap, "adr_scara"),
                GetField(fields, colMap, "adr_etaj"),
                GetField(fields, colMap, "adr_apartament"),
                GetField(fields, colMap, "adr_completare"))
            : GetField(fields, colMap, "address"); // fallback for single-column formats

        batch[cui] = new ONRCFirmRecord
        {
          Id = Guid.NewGuid(),
          Region = region,
          CUI = cui,
          Name = name,
          TradeRegisterNo = GetField(fields, colMap, "traderegisterno"),
          CAEN = GetField(fields, colMap, "caen"),
          Address = address,
          Locality = GetField(fields, colMap, "locality"),
          County = GetField(fields, colMap, "county"),
          PostalCode = GetField(fields, colMap, "postalcode"),
          Phone = GetField(fields, colMap, "phone"),
          // Status field: use explicit status column if present, fall back to legal form
          Status = GetField(fields, colMap, "status") ?? GetField(fields, colMap, "forma_juridica"),
          IncorporationYear = ExtractYear(
              GetField(fields, colMap, "data_inmatriculare")
              ?? GetField(fields, colMap, "incorporationyear")),
          ShareCapitalRon = ParseDecimal(GetField(fields, colMap, "sharecapitalron")),
          ImportedAt = DateTime.UtcNow,
          CreatedOn = DateTime.UtcNow,
          CreatedBy = "ONRCImport",
        };

        if (batch.Count >= BatchSize)
          await FlushBatchAsync();
      }
      catch (Exception ex)
      {
        if (errors.Count < 100)
          errors.Add($"Row {totalRows}: {ex.Message}");
      }
    }

    await FlushBatchAsync(); // Process remaining records

    await _audit.LogAsync(new AuditEntry
    {
      Action = "ONRC Firm Database Import Completed",
      Description = $"ONRC firm database for {region} updated: {totalImported} new, {totalUpdated} updated from CSV ({totalRows} total rows).",
      EntityType = "ONRCFirmDatabase",
      NewValues = new { region = region.ToString(), imported = totalImported, updated = totalUpdated, totalRows, errors = errors.Count },
      Severity = "Info",
      Category = "SystemData",
    });

    _logger.LogInformation("ONRC import for {Region}: {Imported} new, {Updated} updated, {Errors} errors from {TotalRows} rows",
        region, totalImported, totalUpdated, errors.Count, totalRows);

    return new ONRCImportResult
    {
      TotalRows = totalRows,
      Imported = totalImported,
      Updated = totalUpdated,
      Skipped = totalSkipped,
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
      // ── Core identifiers ──────────────────────────────────────────────────
      ["cui"]              = ["cui", "cif", "cod_fiscal", "fiscal_code", "cod fiscal"],
      ["name"]             = ["name", "denumire", "firma", "company_name", "company name"],
      ["traderegisterno"]  = ["trade_register_no", "nr_reg_com", "reg_com", "j_number",
                               "Nr. Reg. Com.", "cod_inmatriculare"],
      ["caen"]             = ["caen", "cod_caen", "activity_code"],
      // ── Address — single-column fallback (generic CSV) ────────────────────
      ["address"]          = ["address", "adresa", "sediu"],
      // ── Address — ONRC split-column format ────────────────────────────────
      ["adr_den_strada"]   = ["adr_den_strada", "strada", "street_name"],
      ["adr_nr_strada"]    = ["adr_nr_strada", "nr_strada", "street_no"],
      ["adr_bloc"]         = ["adr_bloc", "bloc"],
      ["adr_scara"]        = ["adr_scara", "scara"],
      ["adr_etaj"]         = ["adr_etaj", "etaj"],
      ["adr_apartament"]   = ["adr_apartament", "apartament"],
      ["adr_completare"]   = ["adr_completare"],
      ["adr_sector"]       = ["adr_sector", "sector"],
      // ── Location ──────────────────────────────────────────────────────────
      ["locality"]         = ["locality", "localitate", "oras", "city", "adr_localitate"],
      ["county"]           = ["county", "judet", "adr_judet"],
      ["postalcode"]       = ["postal_code", "cod_postal", "zip", "Cod Postal", "adr_cod_postal"],
      // ── Contact ───────────────────────────────────────────────────────────
      ["phone"]            = ["phone", "telefon", "tel"],
      // ── Firm metadata ─────────────────────────────────────────────────────
      ["status"]           = ["status", "stare", "stare_firma"],
      ["forma_juridica"]   = ["forma_juridica", "legal_form", "tip_firma"],
      ["data_inmatriculare"] = ["data_inmatriculare", "data_inregistrarii", "incorporation_date"],
      ["incorporationyear"]  = ["incorporation_year", "an_infiintare", "year", "An Infiintare"],
      ["sharecapitalron"]  = ["share_capital", "capital_social", "capital", "Capital Social"],
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

  private static string[] ParseCsvLine(string line, char delimiter = ',')
  {
    var fields = new List<string>();
    var current = new StringBuilder(64);
    var inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
      var c = line[i];
      if (c == '"')
      {
        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
        {
          current.Append('"');
          i++;
        }
        else
        {
          inQuotes = !inQuotes;
        }
      }
      else if (c == delimiter && !inQuotes)
      {
        fields.Add(current.ToString());
        current.Clear();
      }
      else
      {
        current.Append(c);
      }
    }
    fields.Add(current.ToString());
    return fields.ToArray();
  }

  /// <summary>Assembles a human-readable address from ONRC split-column components.</summary>
  private static string? AssembleRoAddress(
      string? street, string? no, string? bloc, string? scara,
      string? etaj, string? ap, string? extra)
  {
    var parts = new List<string>(8);
    if (!string.IsNullOrWhiteSpace(street)) parts.Add($"Str. {street.Trim()}");
    if (!string.IsNullOrWhiteSpace(no))     parts.Add($"Nr. {no.Trim()}");
    if (!string.IsNullOrWhiteSpace(bloc))   parts.Add($"Bl. {bloc.Trim()}");
    if (!string.IsNullOrWhiteSpace(scara))  parts.Add($"Sc. {scara.Trim()}");
    if (!string.IsNullOrWhiteSpace(etaj))   parts.Add($"Et. {etaj.Trim()}");
    if (!string.IsNullOrWhiteSpace(ap))     parts.Add($"Ap. {ap.Trim()}");
    if (!string.IsNullOrWhiteSpace(extra))  parts.Add(extra.Trim());
    var result = string.Join(", ", parts);
    return string.IsNullOrWhiteSpace(result) ? null : result;
  }

  /// <summary>
  /// Extracts a 4-digit year string from either an already-numeric year or a
  /// date formatted as dd/MM/yyyy (ONRC) or yyyy-MM-dd (ISO).
  /// </summary>
  private static string? ExtractYear(string? value)
  {
    if (string.IsNullOrWhiteSpace(value)) return null;
    // Already a 4-digit year string
    if (value.Length == 4 && value.All(char.IsDigit)) return value;
    // dd/MM/yyyy — ONRC DATA_INMATRICULARE format
    if (DateTime.TryParseExact(value, "dd/MM/yyyy",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt.Year.ToString();
    // yyyy-MM-dd ISO
    if (DateTime.TryParseExact(value, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2)) return dt2.Year.ToString();
    // Last resort: extract first plausible 4-digit year
    var m = System.Text.RegularExpressions.Regex.Match(value, @"\b(19|20)\d{2}\b");
    return m.Success ? m.Value : null;
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
