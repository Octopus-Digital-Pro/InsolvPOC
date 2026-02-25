using Insolvex.Domain.Entities;

namespace Insolvex.Core.Abstractions;

public interface IAuthorizationService
{
    bool CanAccessTenant(Guid tenantId);
    bool CanManageUsers();
    bool CanViewAuditLogs();
    bool CanManageSystemSettings();
}
