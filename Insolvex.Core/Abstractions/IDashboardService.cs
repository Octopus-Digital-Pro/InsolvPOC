using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for the practitioner dashboard: aggregate case stats, deadlines, calendar, recent tasks.
/// </summary>
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
