using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

public interface ICaseDeadlineService
{
    Task<IReadOnlyList<CaseDeadlineDto>> GetByCaseAsync(Guid caseId, CancellationToken ct = default);
    Task<CaseDeadlineDto> CreateAsync(Guid caseId, CreateCaseDeadlineBody body, CancellationToken ct = default);
    Task<CaseDeadlineDto?> UpdateAsync(Guid caseId, Guid id, UpdateCaseDeadlineBody body, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid caseId, Guid id, CancellationToken ct = default);
}
