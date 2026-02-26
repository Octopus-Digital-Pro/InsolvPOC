namespace Insolvex.Domain.Entities;

public class Company : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CuiRo { get; set; }
    public string? TradeRegisterNo { get; set; }
    public string? VatNumber { get; set; }
    public string? Address { get; set; }
    public string? Locality { get; set; }
    public string? County { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Caen { get; set; }
    public string? IncorporationYear { get; set; }
    public decimal? ShareCapitalRon { get; set; }

    // Contact
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }

    // Banking
    public string? Iban { get; set; }
    public string? BankName { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public virtual User? AssignedTo { get; set; }

    // Navigation
    public ICollection<CaseParty> CaseParties { get; set; } = new List<CaseParty>();
    public ICollection<CompanyTask> Tasks { get; set; } = new List<CompanyTask>();
}
