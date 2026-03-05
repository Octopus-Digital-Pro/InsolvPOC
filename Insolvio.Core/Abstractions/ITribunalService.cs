using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>Application service for managing court / tribunal reference data.</summary>
public interface ITribunalService
{
    /// <summary>List all tribunals visible to the current user.</summary>
    Task<List<TribunalDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Get a single tribunal by ID. Returns null if not found. Throws ForbiddenException if the user cannot access it.</summary>
    Task<TribunalDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Create a new tribunal record (global if GlobalAdmin, tenant-scoped otherwise).</summary>
    Task<TribunalDto> CreateAsync(TribunalRequest request, CancellationToken ct = default);

    /// <summary>Update an existing tribunal record.</summary>
    Task<TribunalDto> UpdateAsync(Guid id, TribunalRequest request, CancellationToken ct = default);

    /// <summary>Delete a tribunal record.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bulk-import tribunal records from a CSV stream.</summary>
    Task<AuthorityImportResult> ImportCsvAsync(Stream csvStream, CancellationToken ct = default);

    /// <summary>Export tribunal records to a CSV byte array.</summary>
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}
