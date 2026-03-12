namespace Insolvio.Core.DTOs;

/// <summary>Data transfer object for a Region.</summary>
public record RegionDto(
    Guid Id,
    string Name,
    string IsoCode,
    string Flag,
    int UsageCount,
    bool IsDefault
);
