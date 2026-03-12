import { useState } from "react";
import { Link, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import {
  ArrowLeft, Building2, Users, KeyRound,
  Mail, AlertCircle, ShieldCheck, Gavel, Receipt, MapPin,
  RotateCcw, Database, Clock, FileText, Brain, Layers, Key, Menu, X, Globe,
} from "lucide-react";

interface SettingsNavItemProps {
  to: string;
  icon: React.ElementType;
  label: string;
}

function SettingsNavItem({ to, icon: Icon, label }: SettingsNavItemProps) {
  const { pathname } = useLocation();
  const isActive = pathname === to;

  return (
    <Link
      to={to}
      className={`flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${isActive ? "bg-primary/10 text-primary" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span className="truncate">{label}</span>
    </Link>
  );
}

export default function SettingsLayout() {
const { isGlobalAdmin, isTenantAdmin } = useAuth();
  const { t } = useTranslation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <div className="flex h-full -m-4 md:-m-6">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 md:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Settings sidebar */}
      <aside className={`
        fixed inset-y-0 left-0 z-50 w-64 transform transition-transform duration-200 ease-in-out
        md:relative md:translate-x-0 md:z-auto
        ${sidebarOpen ? "translate-x-0" : "-translate-x-full"}
        shrink-0 border-r border-border bg-card overflow-y-auto
      `}>
        {/* Back button */}
 <div className="px-3 py-4 border-b border-border">
     <Link
            to="/dashboard"
          className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
          onClick={() => setSidebarOpen(false)}
   >
         <ArrowLeft className="h-4 w-4" />
            {t.settings.backToApp}
  </Link>
        </div>

     <nav className="p-3 space-y-1">
    {/* General */}
      <div className="mb-1 px-3 pt-2 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/50">
            {t.settings.general}
          </div>
          {/* Organisation now includes firm details — no separate Firm Details tab */}
       <SettingsNavItem to="/settings" icon={Building2} label={t.settings.organization} />
   <SettingsNavItem to="/settings/users" icon={Users} label={t.settings.teamUsers} />
        <SettingsNavItem to="/settings/signing" icon={KeyRound} label={t.settings.eSigning} />

          {/* Data */}
        <div className="mb-1 px-3 pt-4 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/50">
         {t.common.records ?? "Data"}
      </div>
          <SettingsNavItem to="/settings/firms-database" icon={Database} label={t.settings.firmsDatabase} />
          <SettingsNavItem to="/settings/tribunals" icon={Gavel} label={t.authorities?.tribunals ?? "Tribunals"} />
<SettingsNavItem to="/settings/finance" icon={Receipt} label={t.authorities?.finance ?? "Finance"} />
    <SettingsNavItem to="/settings/localgov" icon={MapPin} label={t.authorities?.localGov ?? "Local Gov"} />
        <SettingsNavItem to="/settings/deadlines" icon={Clock} label={t.tasks?.deadline ?? "Deadlines"} />
          <SettingsNavItem to="/settings/templates" icon={FileText} label={t.templateSettings.pageTitle} />
          <SettingsNavItem to="/settings/workflow-stages" icon={Layers} label={t.settings.casePhases} />
          {/* System */}
     <div className="mb-1 px-3 pt-4 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/50">
    {t.nav.admin}
          </div>
     <SettingsNavItem to="/settings/emails" icon={Mail} label={t.settings.scheduledEmails} />
       <SettingsNavItem to="/settings/email-preferences" icon={Mail} label={t.emailSettings?.pageTitle ?? "Email Preferences"} />
       <SettingsNavItem to="/settings/integrations" icon={Globe} label={t.integrations?.pageTitle ?? "Integrations"} />
       <SettingsNavItem to="/settings/errors" icon={AlertCircle} label={t.settings.errorLogs} />
      <SettingsNavItem to="/settings/permissions" icon={ShieldCheck} label={t.settings.permissions} />
          {isGlobalAdmin && (
            <SettingsNavItem to="/settings/demo" icon={RotateCcw} label={t.settings.demoReset} />
          )}
          {isGlobalAdmin && (
            <SettingsNavItem to="/settings/ai-config" icon={Brain} label={t.settings.aiConfiguration} />
          )}
          {isGlobalAdmin && (
            <SettingsNavItem to="/settings/tenant-ai" icon={Layers} label={t.ai.tenantConfigTitle} />
          )}
          {isTenantAdmin && !isGlobalAdmin && (
            <SettingsNavItem to="/settings/my-ai" icon={Key} label="My AI Settings" />
          )}
        </nav>
      </aside>

      {/* Settings content */}
 <div className="flex-1 overflow-y-auto">
        {/* Mobile top bar for settings */}
        <div className="flex items-center gap-2 border-b border-border bg-background px-4 py-2 md:hidden">
          <button
            onClick={() => setSidebarOpen(!sidebarOpen)}
            className="rounded-md p-1.5 text-muted-foreground hover:bg-accent"
          >
            {sidebarOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
          </button>
          <span className="text-sm font-semibold text-foreground">{t.nav.settings}</span>
        </div>
        <div className="p-6">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
