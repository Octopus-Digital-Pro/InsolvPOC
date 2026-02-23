using System.Security.Claims;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

  public Guid? UserId
 {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      return claim != null ? Guid.Parse(claim) : null;
     }
    }

    public Guid? TenantId
 {
        get
        {
  // From middleware context items (for global admins switching tenants)
      if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var contextTenantId) == true)
 {
         return (Guid?)contextTenantId;
   }

      // From JWT claims (for regular users)
  var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
            return claim != null ? Guid.Parse(claim) : null;
        }
 }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public UserRole Role
 {
     get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
      return Enum.TryParse<UserRole>(claim, out var role) ? role : UserRole.Secretary;
      }
    }

    public bool IsGlobalAdmin => Role == UserRole.GlobalAdmin;

  public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
