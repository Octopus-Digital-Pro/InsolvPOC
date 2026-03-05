using Insolvio.Domain.Entities;

namespace Insolvio.Core.Abstractions;

public interface IAuthorizationService
{
    bool CanAccessTenant(Guid tenantId);
    bool CanManageUsers();
    bool CanViewAuditLogs();
    bool CanManageSystemSettings();
}
