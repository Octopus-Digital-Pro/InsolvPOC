using System.Security.Claims;
using Insolvio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Insolvio.API.Authorization;

/// <summary>
/// Requirement that the user has a specific Permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public Permission Permission { get; }

    public PermissionRequirement(Permission permission)
    {
     Permission = permission;
    }
}

/// <summary>
/// Handler that checks the user's role claim against the static RolePermissions map.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (roleClaim != null && Enum.TryParse<UserRole>(roleClaim, true, out var role))
        {
            if (RolePermissions.HasPermission(role, requirement.Permission))
       {
    context.Succeed(requirement);
            }
   }

   return Task.CompletedTask;
    }
}
