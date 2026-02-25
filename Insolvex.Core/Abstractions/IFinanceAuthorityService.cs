using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>Application service for managing ANAF finance authority reference data.</summary>
public interface IFinanceAuthorityService
{
    Task<List<AuthorityDto>> GetAllAsync(CancellationToken ct = default);
    Task<AuthorityDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuthorityDto> CreateAsync(FinanceAuthorityRequest request, CancellationToken ct = default);
    Task<AuthorityDto> UpdateAsync(Guid id, FinanceAuthorityRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<AuthorityImportResult> ImportCsvAsync(Stream csvStream, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}
