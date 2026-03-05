using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

public record UpdateUserRequest(string? FirstName, string? LastName, UserRole? Role, bool? IsActive);
public record InviteUserRequest(string Email, string FirstName, string LastName, string Role);
public record AcceptInvitationRequest(string Token, string Password);
public record AdminResetPasswordRequest(string NewPassword);

public record UserInvitationDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsAccepted,
    DateTime? AcceptedAt,
    DateTime ExpiresAt,
    DateTime CreatedOn
);
