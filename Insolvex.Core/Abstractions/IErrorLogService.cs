using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Service for reading and resolving application error logs.
/// </summary>
public interface IErrorLogService
{
    Task<(List<ErrorLogDto> Items, int Total)> GetAllAsync(
        int page = 0, int pageSize = 50, bool? resolved = null, CancellationToken ct = default);
    Task CreateClientErrorAsync(CreateClientErrorLogRequest request, string? userId, string? userEmail, CancellationToken ct = default);
    Task ResolveAsync(Guid id, CancellationToken ct = default);
}
