using Insolvio.Domain.Enums;

namespace Insolvio.Core.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }
    UserRole Role { get; }
    bool IsGlobalAdmin { get; }
    bool IsAuthenticated { get; }
}
