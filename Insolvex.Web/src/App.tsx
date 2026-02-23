import { BrowserRouter, Routes, Route, Navigate, Outlet } from "react-router-dom";
import { AuthProvider, useAuth } from "./contexts/AuthContext";
import { TenantProvider } from "./contexts/TenantContext";
import { LanguageProvider } from "./contexts/LanguageContext";
import SidebarNav from "./components/SidebarNav";
import LoginPage from "./pages/LoginPage";
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
import AuditTrailPage from "./pages/AuditTrailPage";
import TenantAdminPage from "./pages/TenantAdminPage";
import ErrorBoundary from "./components/ErrorBoundary";
import { Loader2 } from "lucide-react";

function ProtectedLayout() {
  const { isAuthenticated, loading } = useAuth();

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
        <SidebarNav />
        <main className="flex-1 overflow-y-auto bg-background p-6">
          <Outlet />
        </main>
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
        <Route path="/documents/:id/review" element={<DocumentReviewPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/audit-trail" element={<AuditTrailPage />} />
        <Route path="/admin/tenants" element={<TenantAdminPage />} />
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
     <LanguageProvider>
      <AuthProvider>
      <AppRoutes />
          </AuthProvider>
        </LanguageProvider>
      </BrowserRouter>
    </ErrorBoundary>
  );
}
