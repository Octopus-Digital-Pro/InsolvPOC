namespace Insolvex.Domain.Entities;

/// <summary>
/// An asset identified during the insolvency procedure.
/// Used in Phase 6 (Asset Investigation &amp; Liquidation) for inventory,
/// valuation, sale, and distribution reporting.
/// </summary>
public class Asset : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  /// <summary>Vehicle, RealEstate, Receivable, Inventory, Equipment, IP, Cash, Other.</summary>
  public string AssetType { get; set; } = "Other";

  /// <summary>Human-readable description of the asset.</summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>Estimated value in RON.</summary>
  public decimal? EstimatedValue { get; set; }

  /// <summary>Details about any encumbrance (secured creditor, lien).</summary>
  public string? EncumbranceDetails { get; set; }

  /// <summary>Reference to the secured creditor party, if any.</summary>
  public Guid? SecuredCreditorPartyId { get; set; }
  public virtual CaseParty? SecuredCreditorParty { get; set; }

  /// <summary>Identified / Valued / ForSale / Sold / Unrecoverable.</summary>
  public string Status { get; set; } = "Identified";

  /// <summary>Actual sale proceeds in RON (after auction/sale).</summary>
  public decimal? SaleProceeds { get; set; }

  /// <summary>Date the asset was sold or declared unrecoverable.</summary>
  public DateTime? DisposedAt { get; set; }

  /// <summary>Notes about the asset (location, condition, etc.).</summary>
  public string? Notes { get; set; }
}
