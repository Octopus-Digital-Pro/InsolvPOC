using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

public interface IRegionService
{
    Task<List<RegionDto>> GetAllAsync(CancellationToken ct = default);
    Task<RegionDto> CreateAsync(string name, string isoCode, string flag, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<RegionDto> SetDefaultAsync(Guid id, CancellationToken ct = default);
}
