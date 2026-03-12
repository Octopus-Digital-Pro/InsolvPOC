import { useState } from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  Outlet,
} from "react-router-dom";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { TenantProvider } from "./contexts/TenantContext";
import { LanguageProvider } from "./contexts/LanguageContext";
import SidebarNav from "./components/SidebarNav";
import SettingsLayout from "./components/SettingsLayout";
import LoginPage from "./pages/LoginPage";
import AcceptInvitationPage from "./pages/AcceptInvitationPage";
import DashboardPage from "./pages/DashboardPage";
import CasesPage from "./pages/CasesPage";
import CaseDetailPage from "./pages/CaseDetailPage";
import NewCasePage from "./pages/NewCasePage";
import CompaniesPage from "./pages/CompaniesPage";
import CompanyDetailPage from "./pages/CompanyDetailPage";
import { NewCompanyPage, EditCompanyPage } from "./pages/CompanyFormPages";
import TasksPage from "./pages/TasksPage";
import DocumentReviewPage from "./pages/DocumentReviewPage";
import SettingsPage from "./pages/SettingsPage";
import DeadlineSettingsPage from "./pages/DeadlineSettingsPage";
import TemplateSettingsPage from "./pages/TemplateSettingsPage";
import OrganisationSettingsPage from "./pages/OrganisationSettingsPage";
import AuditTrailPage from "./pages/AuditTrailPage";
import TenantAdminPage from "./pages/TenantAdminPage";
import RegionsPage from "./pages/RegionsPage";
import ONRCSettingsPage from "./pages/ONRCSettingsPage";
import AiSettingsPage from "./pages/AiSettingsPage";
import TenantAiConfigPage from "./pages/TenantAiConfigPage";
import MyAiSettingsPage from "./pages/MyAiSettingsPage";
import WorkflowStagesPage from "./pages/WorkflowStagesPage";
import ReportsPage from "./pages/ReportsPage";
import EmailSettingsPage from "./pages/EmailSettingsPage";
import IntegrationsSettingsPage from "./pages/IntegrationsSettingsPage";
import ErrorBoundary from "./components/ErrorBoundary";
import { Loader2, Menu, X } from "lucide-react";

function ProtectedLayout() {
  const { isAuthenticated, loading } = useAuth();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return (
    <TenantProvider>
      <div className="flex h-screen overflow-hidden">
        {/* Mobile overlay */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-40 bg-black/50 md:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}

        {/* Sidebar — hidden on mobile, visible on md+ */}
        <div
          className={`
          fixed inset-y-0 left-0 z-50 w-64 transform transition-transform duration-200 ease-in-out md:relative md:translate-x-0
       ${sidebarOpen ? "translate-x-0" : "-translate-x-full"}`}
        >
          <SidebarNav />
        </div>

        {/* Main content */}
        <div className="flex flex-1 flex-col min-w-0">
          {/* Mobile top bar */}
          <div className="flex items-center gap-2 border-b border-border bg-background px-4 py-2 md:hidden">
            <button
              onClick={() => setSidebarOpen(!sidebarOpen)}
              className="rounded-md p-1.5 text-muted-foreground hover:bg-accent"
            >
              {sidebarOpen ? (
                <X className="h-5 w-5" />
              ) : (
                <Menu className="h-5 w-5" />
              )}
            </button>
            <span className="text-sm font-semibold text-foreground">
              Insolvio
            </span>
          </div>

          <main className="flex-1 overflow-y-auto bg-background p-4 md:p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </TenantProvider>
  );
}

function PublicRoute() {
  const { isAuthenticated, loading } = useAuth();

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Outlet />;
}

function AppRoutes() {
  return (
    <Routes>
      {/* Public */}
      <Route element={<PublicRoute />}>
        <Route path="/login" element={<LoginPage />} />
      </Route>

      {/* Standalone public — no auth redirect */}
      <Route path="/accept-invitation" element={<AcceptInvitationPage />} />

      {/* Protected */}
      <Route element={<ProtectedLayout />}>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/cases" element={<CasesPage />} />
        <Route path="/cases/new" element={<NewCasePage />} />
        <Route path="/cases/:id" element={<CaseDetailPage />} />
        <Route path="/companies" element={<CompaniesPage />} />
        <Route path="/companies/new" element={<NewCompanyPage />} />
        <Route path="/companies/:id" element={<CompanyDetailPage />} />
        <Route path="/companies/:id/edit" element={<EditCompanyPage />} />
        <Route path="/tasks" element={<TasksPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/documents/:id/review" element={<DocumentReviewPage />} />

        {/* Settings — sidebar takeover layout */}
        <Route path="/settings" element={<SettingsLayout />}>
          <Route index element={<OrganisationSettingsPage />} />
          <Route path="users" element={<SettingsPage tab="users" />} />
          <Route path="signing" element={<SettingsPage tab="signing" />} />
          <Route path="firms-database" element={<ONRCSettingsPage />} />
          <Route path="tribunals" element={<SettingsPage tab="tribunals" />} />
          <Route path="finance" element={<SettingsPage tab="finance" />} />
          <Route path="localgov" element={<SettingsPage tab="localgov" />} />
          <Route path="deadlines" element={<DeadlineSettingsPage />} />
          <Route path="templates" element={<TemplateSettingsPage />} />
          <Route path="workflow-stages" element={<WorkflowStagesPage />} />
          <Route path="emails" element={<SettingsPage tab="emails" />} />
          <Route path="email-preferences" element={<EmailSettingsPage />} />
          <Route path="integrations" element={<IntegrationsSettingsPage />} />
          <Route path="errors" element={<SettingsPage tab="errors" />} />
          <Route
            path="permissions"
            element={<SettingsPage tab="permissions" />}
          />
          <Route path="demo" element={<SettingsPage tab="demo" />} />
          <Route path="ai-config" element={<AiSettingsPage />} />
          <Route path="tenant-ai" element={<TenantAiConfigPage />} />
          <Route path="my-ai" element={<MyAiSettingsPage />} />
          {/* Legacy redirects */}
          <Route path="firm" element={<OrganisationSettingsPage />} />
          <Route path="onrc" element={<ONRCSettingsPage />} />
        </Route>

        <Route path="/audit-trail" element={<AuditTrailPage />} />
        <Route path="/admin/tenants" element={<TenantAdminPage />} />
        <Route path="/admin/regions" element={<RegionsPage />} />
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <LanguageProvider>
        <AuthProvider>
          <ErrorBoundary>
            <AppRoutes />
          </ErrorBoundary>
        </AuthProvider>
      </LanguageProvider>
    </BrowserRouter>
  );
}
