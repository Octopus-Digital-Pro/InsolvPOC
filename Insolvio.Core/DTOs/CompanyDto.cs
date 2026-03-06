namespace Insolvio.Core.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    string? CuiRo,
    string? TradeRegisterNo,
    string? VatNumber,
    string? Address,
    string? Locality,
    string? County,
    string? Country,
    string? PostalCode,
    string? Caen,
    string? IncorporationYear,
    decimal? ShareCapitalRon,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Iban,
    string? BankName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedOn,
    int CaseCount,
    List<string>? CaseNumbers
);

public record CreateCompanyRequest(
    string Name,
    string? CuiRo,
    string? TradeRegisterNo,
    string? VatNumber,
    string? Address,
    string? Locality,
    string? County,
    string? Country,
    string? PostalCode,
    string? Caen,
    string? IncorporationYear,
    decimal? ShareCapitalRon,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Iban,
    string? BankName
);

public record UpdateCompanyRequest(
    string? Name,
    string? CuiRo,
    string? TradeRegisterNo,
    string? VatNumber,
    string? Address,
    string? Locality,
    string? County,
    string? Country,
    string? PostalCode,
    string? Caen,
    string? IncorporationYear,
    decimal? ShareCapitalRon,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Iban,
    string? BankName,
    Guid? AssignedToUserId
);
