namespace Insolvex.Core.DTOs;

public record CasePartyDto(
    Guid Id,
    Guid CaseId,
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
