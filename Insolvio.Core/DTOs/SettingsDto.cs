namespace Insolvio.Core.DTOs;

public record SystemConfigDto(string Key, string Value, string? Description, string? Group);
public record UpdateSystemConfigRequest(List<SystemConfigItemRequest> Items);
public record SystemConfigItemRequest(string Key, string Value, string? Description, string? Group);

public record UpdateTenantSettingsRequest(string? Name, string? Domain, string? Region, string? Language = null);

public record CreateScheduledEmailRequest(
    string To,
    string Subject,
    string Body,
    string? Cc = null,
    DateTime? ScheduledFor = null
);

public record ScheduledEmailDto(
    Guid Id,
    string To,
    string? Cc,
    string Subject,
    string Body,
    DateTime ScheduledFor,
    DateTime? SentAt,
    bool IsSent,
    string Status,
    string? ErrorMessage,
    DateTime CreatedOn
);
