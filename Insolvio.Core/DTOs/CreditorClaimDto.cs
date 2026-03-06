namespace Insolvio.Core.DTOs;

public record CreditorClaimDto(
    Guid Id,
    Guid CaseId,
    Guid CreditorPartyId,
    string CreditorName,
    string? CreditorIdentifier,
    string CreditorRole,
    int RowNumber,
    decimal DeclaredAmount,
    decimal? AdmittedAmount,
    string Rank,
    string? NatureDescription,
    string Status,
    DateTime? ReceivedAt,
    string? Notes,
    DateTime CreatedOn
);

public record CreateCreditorClaimRequest(
    Guid CreditorPartyId,
    decimal DeclaredAmount,
    string? Rank,
    string? NatureDescription,
    string? Status,
    DateTime? ReceivedAt,
    string? Notes
);

public record UpdateCreditorClaimRequest(
    decimal? DeclaredAmount,
    decimal? AdmittedAmount,
    string? Rank,
    string? NatureDescription,
    string? Status,
    DateTime? ReceivedAt,
    string? Notes
);
