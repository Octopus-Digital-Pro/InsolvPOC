using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for case party (creditor / debtor / practitioner) management.
/// All mutations are audited.
/// </summary>
public interface ICasePartyService
{
    Task<List<CasePartyDto>> GetAllAsync(Guid caseId, CancellationToken ct = default);
    Task<CasePartyDto> GetByIdAsync(Guid caseId, Guid partyId, CancellationToken ct = default);
    Task<CasePartyDto> CreateAsync(Guid caseId, CreateCasePartyRequest request, CancellationToken ct = default);
    Task<CasePartyDto> UpdateAsync(Guid caseId, Guid partyId, UpdateCasePartyRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid caseId, Guid partyId, CancellationToken ct = default);

    Task<List<CompanyCasePartyDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
}
