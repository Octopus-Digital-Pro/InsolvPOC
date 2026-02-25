using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for case phase lifecycle management (initialize, advance, task generation).
/// All mutations are audited.
/// </summary>
public interface ICasePhasesService
{
    Task<List<CasePhaseDto>> GetAllAsync(Guid caseId, CancellationToken ct = default);
    Task<List<CasePhaseDto>> InitializeAsync(Guid caseId, CancellationToken ct = default);
    Task<CasePhaseDto> UpdateAsync(Guid caseId, Guid phaseId, UpdateCasePhaseRequest request, CancellationToken ct = default);
    Task<CasePhaseDto> AdvanceAsync(Guid caseId, CancellationToken ct = default);
    Task<object> GetRequirementsAsync(Guid caseId, Guid phaseId, CancellationToken ct = default);
    Task<int> GenerateTasksAsync(Guid caseId, Guid phaseId, CancellationToken ct = default);
}
