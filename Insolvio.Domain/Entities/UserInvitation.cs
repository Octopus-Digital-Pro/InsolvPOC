using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

public class UserInvitation : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? InvitedByUserId { get; set; }
    public virtual User? InvitedBy { get; set; }
}
