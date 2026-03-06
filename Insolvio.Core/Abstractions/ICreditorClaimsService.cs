using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

public interface ICreditorClaimsService
{
    Task<List<CreditorClaimDto>> GetAllAsync(Guid caseId, CancellationToken ct = default);
    Task<CreditorClaimDto> GetByIdAsync(Guid caseId, Guid claimId, CancellationToken ct = default);
    Task<CreditorClaimDto> CreateAsync(Guid caseId, CreateCreditorClaimRequest request, CancellationToken ct = default);
    Task<CreditorClaimDto> UpdateAsync(Guid caseId, Guid claimId, UpdateCreditorClaimRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid caseId, Guid claimId, CancellationToken ct = default);
}
