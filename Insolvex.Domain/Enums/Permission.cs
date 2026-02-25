namespace Insolvex.Domain.Enums;

/// <summary>
/// Granular permissions for RBAC. Each permission maps to a specific action
/// on a specific entity type. Controllers use [RequirePermission(Permission.X)]
/// instead of [Authorize(Roles = "...")].
/// </summary>
public enum Permission
{
  // ?? Cases ??
  CaseView = 100,
  CaseCreate = 101,
  CaseEdit = 102,
  CaseDelete = 103,
  CaseExport = 104,

  // ?? Case Parties ??
  PartyView = 200,
  PartyCreate = 201,
  PartyEdit = 202,
  PartyDelete = 203,

  // ?? Case Phases / Workflow ??
  PhaseView = 300,
  PhaseEdit = 301,
  PhaseInitialize = 302,
  PhaseAdvance = 303,

  // ?? Stage Transitions (Workflow Definition) ??
  StageView = 350,
  StageAdvance = 351,

  // ?? Documents ??
  DocumentView = 400,
  DocumentUpload = 401,
  DocumentEdit = 402,
  DocumentDelete = 403,
  DocumentDownload = 404,

  // ?? Document Signing ??
  SigningKeyManage = 450,
  DocumentSign = 451,
  SignatureVerify = 452,

  // ?? Tasks ??
  TaskView = 500,
  TaskCreate = 501,
  TaskEdit = 502,
  TaskDelete = 503,

  // ?? Companies ??
  CompanyView = 600,
  CompanyCreate = 601,
  CompanyEdit = 602,
  CompanyDelete = 603,

  // ?? Creditor Meetings ??
  MeetingView = 700,
  MeetingCreate = 701,

  // ?? Mail Merge / Templates ??
  TemplateView = 800,
  TemplateGenerate = 801,
  TemplateManage = 802,

  // ?? AI Summaries ??
  SummaryView = 850,
  SummaryGenerate = 851,

  // ?? Settings ??
  SettingsView = 900,
  SettingsEdit = 901,
  SystemConfigView = 910,
  SystemConfigEdit = 911,
  DemoReset = 912,

  // ?? Users ??
  UserView = 950,
  UserCreate = 951,
  UserEdit = 952,
  UserDeactivate = 953,
  UserInvite = 954,

  // ?? Scheduled Emails ??
  EmailView = 970,
  EmailCreate = 971,
  EmailDelete = 972,

  // ?? Error Logs ??
  ErrorLogView = 980,
  ErrorLogResolve = 981,

  // ?? Audit Logs ??
  AuditLogView = 990,

  // ?? Dashboard ??
  DashboardView = 995,
}
