namespace Insolvex.Core.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string? Domain,
    bool IsActive,
    DateTime? SubscriptionExpiry,
    string? PlanName
);

public record CreateTenantRequest(string Name, string? Domain, string? PlanName);
public record UpdateTenantRequest(string? Name, string? Domain, bool? IsActive, string? PlanName, DateTime? SubscriptionExpiry = null);
