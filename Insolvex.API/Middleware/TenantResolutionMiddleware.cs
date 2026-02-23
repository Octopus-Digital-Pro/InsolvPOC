using System.Security.Claims;

namespace Insolvex.API.Middleware;

/// <summary>
/// Resolves the current tenant context for every request.
/// - Regular users: TenantId from JWT claims (immutable).
/// - GlobalAdmins: TenantId from X-Tenant-Id header (switchable).
/// 
/// SECURITY: No data endpoint should ever execute without a resolved TenantId
/// (except explicit cross-tenant admin endpoints that use IgnoreQueryFilters).
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
  {
     _next = next;
  }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == "GlobalAdmin")
          {
         // GlobalAdmin: tenant from header is REQUIRED for data access
           var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
 ?? context.Request.Query["tenantId"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var parsed))
  {
        context.Items["TenantId"] = parsed;
                }
          // If no tenant header: TenantId stays null.
         // Query filters will return empty results (safe default).
    // Only IgnoreQueryFilters() endpoints (Tenants, SystemConfig) work without it.
   }
        else
      {
         // Regular users: tenant from JWT claims (cannot be overridden)
  var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;
       if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var parsed))
        {
    context.Items["TenantId"] = parsed;
   }
            }

  // Store the resolved tenant ID for audit trail
          if (context.Items.TryGetValue("TenantId", out var resolvedTenantId))
      {
                context.Items["ResolvedTenantId"] = resolvedTenantId;
            }
        }

await _next(context);
    }
}
