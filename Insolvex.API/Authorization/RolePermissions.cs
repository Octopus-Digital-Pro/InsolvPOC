using Insolvex.Domain.Enums;

namespace Insolvex.API.Authorization;

/// <summary>
/// Static role-to-permission mapping. Defines what each UserRole can do.
/// GlobalAdmin: everything. TenantAdmin: everything except system config / demo reset.
/// Practitioner: full case work. Secretary: view + limited create/edit.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<UserRole, HashSet<Permission>> Map = new()
    {
        [UserRole.GlobalAdmin] = new(Enum.GetValues<Permission>()), // all permissions

        [UserRole.TenantAdmin] = new(Enum.GetValues<Permission>()
      .Except(new[]
            {
    Permission.SystemConfigView,
    Permission.SystemConfigEdit,
            Permission.DemoReset,
    })),

      [UserRole.Practitioner] = new(new[]
        {
          // Cases – full CRUD
     Permission.CaseView, Permission.CaseCreate, Permission.CaseEdit, Permission.CaseExport,
            // Parties
            Permission.PartyView, Permission.PartyCreate, Permission.PartyEdit, Permission.PartyDelete,
       // Phases / Workflow
 Permission.PhaseView, Permission.PhaseEdit, Permission.PhaseInitialize, Permission.PhaseAdvance,
            Permission.StageView, Permission.StageAdvance,
            // Documents
  Permission.DocumentView, Permission.DocumentUpload, Permission.DocumentEdit, Permission.DocumentDelete, Permission.DocumentDownload,
 // Signing
  Permission.SigningKeyManage, Permission.DocumentSign, Permission.SignatureVerify,
        // Tasks
            Permission.TaskView, Permission.TaskCreate, Permission.TaskEdit, Permission.TaskDelete,
        // Companies
            Permission.CompanyView, Permission.CompanyCreate, Permission.CompanyEdit,
 // Meetings
          Permission.MeetingView, Permission.MeetingCreate,
    // Templates
            Permission.TemplateView, Permission.TemplateGenerate,
 // Summaries
          Permission.SummaryView, Permission.SummaryGenerate,
            // Settings – view tenant info, firm info, deadline config
Permission.SettingsView,
    // Emails
         Permission.EmailView, Permission.EmailCreate,
            // Error logs – view
          Permission.ErrorLogView,
      // Dashboard
 Permission.DashboardView,
   }),

        [UserRole.Secretary] = new(new[]
        {
       // Cases – view + create (no delete)
            Permission.CaseView, Permission.CaseCreate, Permission.CaseEdit, Permission.CaseExport,
   // Parties – view + create
         Permission.PartyView, Permission.PartyCreate, Permission.PartyEdit,
     // Phases – view only
            Permission.PhaseView,
   Permission.StageView,
     // Documents – view, upload, download (no delete)
        Permission.DocumentView, Permission.DocumentUpload, Permission.DocumentDownload,
            // Signing – can manage own keys and sign
         Permission.SigningKeyManage, Permission.DocumentSign, Permission.SignatureVerify,
    // Tasks – view + create + edit (no delete)
       Permission.TaskView, Permission.TaskCreate, Permission.TaskEdit,
        // Companies – view + create
    Permission.CompanyView, Permission.CompanyCreate,
            // Meetings – view only
   Permission.MeetingView,
   // Templates – view + generate (no manage)
            Permission.TemplateView, Permission.TemplateGenerate,
            // Summaries – view only
        Permission.SummaryView,
          // Settings – view tenant info, firm info, deadline config
            Permission.SettingsView,
   // Emails
            Permission.EmailView, Permission.EmailCreate,
          // Dashboard
  Permission.DashboardView,
        }),
    };

    /// <summary>Check if a role has a specific permission.</summary>
    public static bool HasPermission(UserRole role, Permission permission)
        => Map.TryGetValue(role, out var perms) && perms.Contains(permission);

    /// <summary>Get all permissions for a role.</summary>
    public static IReadOnlySet<Permission> GetPermissions(UserRole role)
        => Map.TryGetValue(role, out var perms) ? perms : new HashSet<Permission>();
}
