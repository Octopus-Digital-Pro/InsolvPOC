namespace Insolvex.Core.DTOs;

public record CasePartyDto(
    Guid Id,
    Guid CaseId,
    Guid CompanyId,
    string? CompanyName,
    string? Email,
 string Role,
    string? RoleDescription,
    decimal? ClaimAmountRon,
    bool? ClaimAccepted,
    DateTime? JoinedDate,
    string? Notes
);

/// <summary>CasePartyDto enriched with case-level info, used when querying all parties by company.</summary>
public record CompanyCasePartyDto(
    Guid Id,
    Guid CaseId,
    string? CaseNumber,
    string? DebtorName,
    Guid CompanyId,
    string? CompanyName,
    string Role,
    string? RoleDescription,
    decimal? ClaimAmountRon,
    bool? ClaimAccepted,
    DateTime? JoinedDate,
    string? Notes
);


public record CreateCasePartyRequest(
    Guid CompanyId,
    string Role,
  string? RoleDescription,
    decimal? ClaimAmountRon,
    bool? ClaimAccepted,
  DateTime? JoinedDate,
    string? Notes
);

public record UpdateCasePartyRequest(
    string? Role,
  string? RoleDescription,
    decimal? ClaimAmountRon,
    bool? ClaimAccepted,
    string? Notes
);
