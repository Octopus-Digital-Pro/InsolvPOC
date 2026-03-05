namespace Insolvio.Core.DTOs;

public record TenantSummaryDto(
    Guid Id,
    string Name,
    string? Domain,
    bool IsActive,
    DateTime? SubscriptionExpiry,
    string? PlanName,
    string? Region,
    int UserCount,
    int CompanyCount,
    int CaseCount
);
