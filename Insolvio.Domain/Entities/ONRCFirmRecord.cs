using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Romanian National Office of the Trade Register (ONRC) firm record.
/// Imported per-region from CSV uploads. Used as the de facto source
/// when searching for companies by CUI or Name anywhere in the system.
/// </summary>
public class ONRCFirmRecord : BaseEntity
{
  /// <summary>Region this record belongs to (e.g. Romania).</summary>
  public SystemRegion Region { get; set; } = SystemRegion.Romania;

  /// <summary>Unique fiscal identifier (CUI / CIF).</summary>
  public string CUI { get; set; } = string.Empty;

  /// <summary>Official company name from ONRC.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Trade register number (e.g. J40/1234/2020).</summary>
  public string? TradeRegisterNo { get; set; }

  /// <summary>CAEN code (activity classification).</summary>
  public string? CAEN { get; set; }

  /// <summary>Registered address.</summary>
  public string? Address { get; set; }

  /// <summary>Locality / city.</summary>
  public string? Locality { get; set; }

  /// <summary>County (Jude?).</summary>
  public string? County { get; set; }

  /// <summary>Postal code.</summary>
  public string? PostalCode { get; set; }

  /// <summary>Phone number from ONRC.</summary>
  public string? Phone { get; set; }

  /// <summary>Status (e.g. "ACTIV", "RADIAT", "DIZOLVARE").</summary>
  public string? Status { get; set; }

  /// <summary>Year of incorporation.</summary>
  public string? IncorporationYear { get; set; }

  /// <summary>Share capital in RON.</summary>
  public decimal? ShareCapitalRon { get; set; }

  /// <summary>When this record was last imported/refreshed.</summary>
  public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
