namespace Insolvex.Core.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string? Domain,
    bool IsActive,
    DateTime? SubscriptionExpiry,
    string? PlanName,
    string? Region = null,
    string? Language = null
);

public record CreateTenantRequest(string Name, string? Domain, string? PlanName,
    string? Region = null, string? Language = null);

public record UpdateTenantRequest(string? Name, string? Domain, bool? IsActive,
    string? PlanName, DateTime? SubscriptionExpiry = null,
    string? Region = null, string? Language = null);
