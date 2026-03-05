using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginDate,
    string? AvatarUrl,
    Guid TenantId,
    bool UseSavedSigningKey
);
