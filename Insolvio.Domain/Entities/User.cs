using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

public class User : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginDate { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? AvatarUrl { get; set; }
    public bool UseSavedSigningKey { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigation
    public ICollection<InsolvencyCase> AssignedCases { get; set; } = new List<InsolvencyCase>();
    public ICollection<CompanyTask> AssignedTasks { get; set; } = new List<CompanyTask>();
    public ICollection<UserInvitation> SentInvitations { get; set; } = new List<UserInvitation>();
    public ICollection<UserSigningKey> SigningKeys { get; set; } = new List<UserSigningKey>();
    public ICollection<DigitalSignature> Signatures { get; set; } = new List<DigitalSignature>();
}
