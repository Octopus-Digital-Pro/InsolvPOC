namespace Insolvex.Core.DTOs;

public record AuditLogDto(
    Guid Id,
    string Action,
    string Description,
    Guid? UserId,
    string? UserEmail,
    string? UserFullName,
    string? TenantName,
    string? EntityType,
    Guid? EntityId,
    string? EntityName,
    string? CaseNumber,
    string? Changes,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? RequestMethod,
    string? RequestPath,
    int? ResponseStatusCode,
    long? DurationMs,
    string Severity,
    string Category,
    string? CorrelationId,
    DateTime Timestamp
);

public record AuditLogFilter(
    string? Action = null,
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Severity = null,
    string? Category = null,
    string? Search = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 0,
    int PageSize = 50
);
