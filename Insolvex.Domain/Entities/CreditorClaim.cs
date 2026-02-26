namespace Insolvex.Domain.Entities;

/// <summary>
/// A creditor's claim against the debtor in an insolvency case.
/// Claims are collected during Phase 1 (Creditor Notification &amp; Claim Collection)
/// and are the basis for the Preliminary and Definitive claim tables.
/// </summary>
public class CreditorClaim : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  /// <summary>The creditor party filing the claim.</summary>
  public Guid CreditorPartyId { get; set; }
  public virtual CaseParty? CreditorParty { get; set; }

  /// <summary>Sequential row number in the claim table.</summary>
  public int RowNumber { get; set; }

  /// <summary>Amount declared by the creditor (RON).</summary>
  public decimal DeclaredAmount { get; set; }

  /// <summary>Amount admitted by the practitioner (RON). Null = not yet reviewed.</summary>
  public decimal? AdmittedAmount { get; set; }

  /// <summary>Claim rank: Secured, Chirographary, Budgetary, Employee, etc.</summary>
  public string Rank { get; set; } = "Chirographary";

  /// <summary>Free-text nature description (e.g. "Furnizare marfuri", "TVA neplătit").</summary>
  public string? NatureDescription { get; set; }

  /// <summary>Received / UnderReview / Admitted / Rejected / NeedsInfo.</summary>
  public string Status { get; set; } = "Received";

  /// <summary>When the claim was received.</summary>
  public DateTime? ReceivedAt { get; set; }

  /// <summary>Who reviewed the claim.</summary>
  public Guid? ReviewedByUserId { get; set; }
  public virtual User? ReviewedBy { get; set; }

  /// <summary>When the claim was reviewed.</summary>
  public DateTime? ReviewedAt { get; set; }

  /// <summary>JSON array of supporting document IDs.</summary>
  public string? SupportingDocumentIdsJson { get; set; }

  /// <summary>Notes / observations about the claim.</summary>
  public string? Notes { get; set; }
}
