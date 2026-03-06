namespace Insolvio.Core.DTOs;

public record AssetDto(
    Guid Id,
    Guid CaseId,
    string AssetType,
    string Description,
    decimal? EstimatedValue,
    string? EncumbranceDetails,
    Guid? SecuredCreditorPartyId,
    string? SecuredCreditorName,
    string Status,
    decimal? SaleProceeds,
    DateTime? DisposedAt,
    string? Notes,
    DateTime CreatedOn
);

public record CreateAssetRequest(
    string AssetType,
    string Description,
    decimal? EstimatedValue,
    string? EncumbranceDetails,
    Guid? SecuredCreditorPartyId,
    string? Status,
    string? Notes
);

public record UpdateAssetRequest(
    string? AssetType,
    string? Description,
    decimal? EstimatedValue,
    string? EncumbranceDetails,
    Guid? SecuredCreditorPartyId,
    string? Status,
    decimal? SaleProceeds,
    DateTime? DisposedAt,
    string? Notes
);
