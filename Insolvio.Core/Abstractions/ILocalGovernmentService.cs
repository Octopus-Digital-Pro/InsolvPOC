using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>Application service for managing local government (Primărie) reference data.</summary>
public interface ILocalGovernmentService
{
    Task<List<AuthorityDto>> GetAllAsync(CancellationToken ct = default);
    Task<AuthorityDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuthorityDto> CreateAsync(LocalGovernmentRequest request, CancellationToken ct = default);
    Task<AuthorityDto> UpdateAsync(Guid id, LocalGovernmentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<AuthorityImportResult> ImportCsvAsync(Stream csvStream, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}
