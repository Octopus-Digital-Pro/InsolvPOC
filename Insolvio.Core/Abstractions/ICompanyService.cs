using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for company (debtor, creditor, practitioner) management.
/// All operations are tenant-scoped and audited.
/// </summary>
public interface ICompanyService
{
    Task<List<CompanyDto>> GetAllAsync(int page = 0, int pageSize = 200, CancellationToken ct = default);
    /// <summary>Search companies by name, CUI, or trade register number. Returns up to maxResults matches.</summary>
    Task<List<CompanyDto>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default);
    Task<CompanyDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CompanyDto> CreateAsync(CreateCompanyCommand command, CancellationToken ct = default);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyCommand command, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Export all companies as CSV bytes.</summary>
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}

public class CreateCompanyCommand
{
    public string Name { get; init; } = string.Empty;
    public string? CuiRo { get; init; }
    public string? TradeRegisterNo { get; init; }
    public string? VatNumber { get; init; }
    public string? Address { get; init; }
    public string? Locality { get; init; }
    public string? County { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public string? Caen { get; init; }
    public string? IncorporationYear { get; init; }
    public decimal? ShareCapitalRon { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ContactPerson { get; init; }
    public string? Iban { get; init; }
    public string? BankName { get; init; }
}

public class UpdateCompanyCommand
{
    public string? Name { get; init; }
    public string? CuiRo { get; init; }
    public string? TradeRegisterNo { get; init; }
    public string? VatNumber { get; init; }
    public string? Address { get; init; }
    public string? Locality { get; init; }
    public string? County { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public string? Caen { get; init; }
    public string? IncorporationYear { get; init; }
    public decimal? ShareCapitalRon { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ContactPerson { get; init; }
    public string? Iban { get; init; }
    public string? BankName { get; init; }
    public Guid? AssignedToUserId { get; init; }
}
