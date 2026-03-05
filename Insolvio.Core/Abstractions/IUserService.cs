using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for user account management (CRUD, invitations, password admin).
/// All mutations are audited.
/// </summary>
public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<UserInvitationDto> InviteAsync(InviteUserRequest request, CancellationToken ct = default);
    Task<List<UserInvitationDto>> GetInvitationsAsync(CancellationToken ct = default);
    Task<UserDto> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid id, CancellationToken ct = default);
    Task AdminResetPasswordAsync(Guid id, AdminResetPasswordRequest request, CancellationToken ct = default);
}
