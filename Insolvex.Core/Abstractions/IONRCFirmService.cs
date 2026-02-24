using Insolvex.Domain.Enums;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Service for ONRC (National Office of the Trade Register) firm database operations.
/// Supports CSV upload, search by CUI or Name, and per-region data management.
/// </summary>
public interface IONRCFirmService
{
 /// <summary>Search firms by CUI (exact or prefix match).</summary>
    Task<List<ONRCFirmResult>> SearchByCuiAsync(string cui, SystemRegion region, int maxResults = 10, CancellationToken ct = default);

    /// <summary>Search firms by Name (contains match, case-insensitive).</summary>
    Task<List<ONRCFirmResult>> SearchByNameAsync(string name, SystemRegion region, int maxResults = 10, CancellationToken ct = default);

    /// <summary>Unified search: if query looks like a number, search CUI; otherwise search Name.</summary>
    Task<List<ONRCFirmResult>> SearchAsync(string query, SystemRegion region, int maxResults = 10, CancellationToken ct = default);

    /// <summary>Import firms from a CSV file. Returns count of imported/updated records.</summary>
    Task<ONRCImportResult> ImportFromCsvAsync(Stream csvStream, SystemRegion region, CancellationToken ct = default);

  /// <summary>Get total record count per region.</summary>
    Task<ONRCDatabaseStats> GetStatsAsync(SystemRegion region, CancellationToken ct = default);
}

public class ONRCFirmResult
{
    public Guid Id { get; init; }
    public string CUI { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? TradeRegisterNo { get; init; }
    public string? CAEN { get; init; }
    public string? Address { get; init; }
    public string? Locality { get; init; }
    public string? County { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
  public string? Status { get; init; }
    public string? IncorporationYear { get; init; }
    public decimal? ShareCapitalRon { get; init; }
    public string Region { get; init; } = string.Empty;
}

public record ONRCImportResult
{
    public int TotalRows { get; init; }
    public int Imported { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
  public List<string> Errors { get; init; } = new();
}

public class ONRCDatabaseStats
{
 public string Region { get; init; } = string.Empty;
    public int TotalRecords { get; init; }
    public DateTime? LastImportedAt { get; init; }
}
