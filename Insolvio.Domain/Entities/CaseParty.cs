using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Links a Company to an InsolvencyCase with a specific role.
/// Per InsolvencyAppRules: parties have direct contact info, delivery preferences, and phase bindings.
/// </summary>
public class CaseParty : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  public Guid? CompanyId { get; set; }
  public virtual Company? Company { get; set; }

  public CasePartyRole Role { get; set; }

  /// <summary>Free-text qualifier, e.g. "lichidator judiciar"</summary>
  public string? RoleDescription { get; set; }

  // ?? Direct contact info (can differ from Company master) ??
  /// <summary>Party name (may differ from Company.Name for individuals).</summary>
  public string? Name { get; set; }
  /// <summary>Identifiers: VAT/CUI, reg no.</summary>
  public string? Identifier { get; set; }
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public string? Address { get; set; }

  /// <summary>Preferred delivery method: email, print, both.</summary>
  public string? PreferredDelivery { get; set; }

  // ?? Claim info ??
  public decimal? ClaimAmountRon { get; set; }
  public bool? ClaimAccepted { get; set; }
  /// <summary>Claim priority: Secured, Unsecured, Budgetary, Employee, etc.</summary>
  public string? ClaimPriority { get; set; }

  public DateTime? JoinedDate { get; set; }
  public string? Notes { get; set; }
}
