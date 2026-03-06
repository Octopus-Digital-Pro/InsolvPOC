namespace Insolvio.Domain.Entities;

/// <summary>
/// The insolvency practitioner firm that is the licensee / tenant of the application.
/// Each tenant has exactly one InsolvencyFirm record.
/// This contains the firm's registration, contact, and banking details
/// used in official documents and correspondence.
/// </summary>
public class InsolvencyFirm : BaseEntity
{
  public Guid TenantId { get; set; }
  public virtual Tenant? Tenant { get; set; }

  // Firm identity
  public string FirmName { get; set; } = string.Empty;
  public string? CuiRo { get; set; }
  public string? TradeRegisterNo { get; set; }
  public string? VatNumber { get; set; }

  /// <summary>UNPIR registration number (Registrul Formelor de Organizare)</summary>
  public string? UnpirRegistrationNo { get; set; }

  /// <summary>UNPIR fiscal attribute code (RFO)</summary>
  public string? UnpirRfo { get; set; }

  // Address
  public string? Address { get; set; }
  public string? Locality { get; set; }
  public string? County { get; set; }
  public string? Country { get; set; }
  public string? PostalCode { get; set; }

  // Contact
  public string? Phone { get; set; }
  public string? Fax { get; set; }
  public string? Email { get; set; }
  public string? Website { get; set; }
  public string? ContactPerson { get; set; }

  // Banking
  public string? Iban { get; set; }
  public string? BankName { get; set; }

  /// <summary>Secondary IBAN (e.g., for client funds / cont de lichidare)</summary>
  public string? SecondaryIban { get; set; }
  public string? SecondaryBankName { get; set; }

  /// <summary>Logo URL / path for document generation</summary>
  public string? LogoUrl { get; set; }
}
