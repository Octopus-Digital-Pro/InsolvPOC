using Insolvio.Domain.Enums;

namespace Insolvio.API.Authorization;

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
            Permission.TenantAiConfigEdit, // GlobalAdmin-only
            Permission.RegionManage,        // GlobalAdmin-only
    })),

        [UserRole.Practitioner] = new(new[]
        {
          // Cases — full CRUD
     Permission.CaseView, Permission.CaseCreate, Permission.CaseEdit, Permission.CaseExport, Permission.CaseClose,
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
 // Summaries & AI chat
          Permission.SummaryView, Permission.SummaryGenerate, Permission.AiChatUse,
          Permission.TenantAiConfigView,
            // Assets — full CRUD
            Permission.AssetView, Permission.AssetCreate, Permission.AssetEdit, Permission.AssetDelete,
            // Settings — view tenant info, firm info, deadline config
Permission.SettingsView,
    // Emails
         Permission.EmailView, Permission.EmailCreate,
            // Error logs — view
          Permission.ErrorLogView,
      // Dashboard
 Permission.DashboardView,
            // AI Training & Feedback
            Permission.TrainingView, Permission.TrainingManage, Permission.AiFeedbackCreate,
            // Notifications
            Permission.NotificationView,
            // Signing — can manage own keys and sign
         Permission.SigningKeyManage, Permission.DocumentSign, Permission.SignatureVerify,
    // Tasks — view + create + edit (no delete)
       Permission.TaskView, Permission.TaskCreate, Permission.TaskEdit,
        // Companies — view + create
    Permission.CompanyView, Permission.CompanyCreate,
            // Meetings — view only
   Permission.MeetingView,
   // Templates — view + generate (no manage)
            Permission.TemplateView, Permission.TemplateGenerate,
            // Summaries & AI chat â€" view + use
        Permission.SummaryView, Permission.AiChatUse,
        Permission.TenantAiConfigView,
            // Assets â€" view + create (no delete)
            Permission.AssetView, Permission.AssetCreate, Permission.AssetEdit,
          // Settings — view tenant info, firm info, deadline config
            Permission.SettingsView,
   // Emails
            Permission.EmailView, Permission.EmailCreate,
          // Dashboard
  Permission.DashboardView,
            // AI Training & Feedback — view + create feedback
            Permission.TrainingView, Permission.AiFeedbackCreate,
            // Notifications
            Permission.NotificationView,
        }),
    };

    /// <summary>Check if a role has a specific permission.</summary>
    public static bool HasPermission(UserRole role, Permission permission)
        => Map.TryGetValue(role, out var perms) && perms.Contains(permission);

    /// <summary>Get all permissions for a role.</summary>
    public static IReadOnlySet<Permission> GetPermissions(UserRole role)
        => Map.TryGetValue(role, out var perms) ? perms : new HashSet<Permission>();
}
