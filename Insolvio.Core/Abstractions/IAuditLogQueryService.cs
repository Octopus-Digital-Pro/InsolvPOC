using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Read-only query service for audit logs (distinct from IAuditService which writes them).
/// </summary>
public interface IAuditLogQueryService
{
    Task<(List<AuditLogDto> Items, int Total)> GetAllAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<List<AuditLogDto>> GetForExportAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<int> GetCountAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<object> GetStatsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}
