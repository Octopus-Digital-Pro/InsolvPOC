namespace Insolvex.Domain.Entities;

/// <summary>
/// Finance authority (ANAF - Administra?ia Na?ional? de Administrare Fiscal?) contact information.
/// Romanian: "ANAF"; English: "National Agency for Fiscal Administration"
/// </summary>
public class FinanceAuthority : BaseEntity
{
  public Guid? TenantId { get; set; }
  public virtual Tenant? Tenant { get; set; }

  /// <summary>Office name, e.g. "ANAF Bucure?ti Sector 1"</summary>
  public string Name { get; set; } = string.Empty;

  public string? Locality { get; set; }
  public string? County { get; set; }
  public string? Address { get; set; }
  public string? PostalCode { get; set; }
  public string? Phone { get; set; }
  public string? Fax { get; set; }
  public string? Email { get; set; }
  public string? Website { get; set; }
  public string? ContactPerson { get; set; }
  public string? ScheduleHours { get; set; }
  public string? Notes { get; set; }
  public Guid? OverridesGlobalId { get; set; }
}
