using Insolvex.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Insolvex.API.Authorization;

/// <summary>
/// Attribute to require a specific permission on a controller or action.
/// Usage: [RequirePermission(Permission.CaseCreate)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission_";

    public RequirePermissionAttribute(Permission permission)
        : base($"{PolicyPrefix}{permission}")
    {
    }
}
