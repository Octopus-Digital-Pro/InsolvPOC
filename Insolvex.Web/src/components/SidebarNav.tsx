import { Link, useLocation } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useTenant } from "@/contexts/TenantContext";
import { useTranslation } from "@/contexts/LanguageContext";
import {
  LayoutDashboard, Building2, Briefcase, ListChecks,
  FileText, Settings, LogOut, Upload, ChevronDown,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import logo from "@/assets/logo.png";

interface NavItemProps {
to: string;
  icon: React.ElementType;
  label: string;
}

function NavItem({ to, icon: Icon, label }: NavItemProps) {
  const { pathname } = useLocation();
  const isActive = pathname === to || pathname.startsWith(to + "/");

  return (
    <Link
      to={to}
      className={`
        flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors
        ${isActive
    ? "bg-sidebar-accent text-sidebar-accent-foreground"
    : "text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground"}
`}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span className="truncate">{label}</span>
    </Link>
  );
}

interface SidebarNavProps {
  onUploadClick?: () => void;
}

export default function SidebarNav({ onUploadClick }: SidebarNavProps) {
  const { user, logout, isGlobalAdmin, isTenantAdmin } = useAuth();
  const { selectedTenant, availableTenants, selectTenant } = useTenant();
  const { t } = useTranslation();
  const isAdmin = isGlobalAdmin || isTenantAdmin;

  return (
    <aside className="flex h-full w-64 flex-col border-r border-sidebar-border bg-sidebar">
      {/* Logo */}
   <div className="flex items-center gap-2.5 border-b border-sidebar-border px-4 py-3">
  <img src={logo} alt="Insolvex" className="h-8 w-auto rounded-md" />
 </div>

      {/* Tenant selector (global admins) */}
      {isGlobalAdmin && availableTenants.length > 0 && (
        <div className="border-b border-sidebar-border px-3 py-2">
    <div className="relative">
    <select
  value={selectedTenant?.id ?? ""}
            onChange={(e) => {
  const t = availableTenants.find(t => t.id === e.target.value);
           if (t) selectTenant(t);
       }}
        className="w-full appearance-none rounded-md border border-sidebar-border bg-sidebar px-2.5 py-1.5 pr-7 text-xs text-sidebar-foreground focus:outline-none focus:ring-1 focus:ring-sidebar-ring"
        >
      {availableTenants.map(t => (
 <option key={t.id} value={t.id}>{t.name}</option>
    ))}
          </select>
          <ChevronDown className="pointer-events-none absolute right-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-sidebar-foreground/50" />
     </div>
        </div>
    )}

    {/* Upload button */}
    {onUploadClick && (
        <div className="px-3 pt-3">
          <Button
   variant="outline"
       size="sm"
 className="w-full gap-1.5 text-xs border-dashed border-primary/30 text-primary hover:bg-primary/5 hover:border-primary/50"
    onClick={onUploadClick}
          >
      <Upload className="h-3.5 w-3.5" />
     {t.nav.uploadDocument}
          </Button>
        </div>
      )}

      {/* Navigation */}
      <nav className="flex-1 space-y-0.5 overflow-y-auto px-3 py-3">
 <NavItem to="/dashboard" icon={LayoutDashboard} label={t.nav.dashboard} />
        <NavItem to="/cases" icon={Briefcase} label={t.nav.cases} />
 <NavItem to="/companies" icon={Building2} label={t.nav.companies} />
        <NavItem to="/tasks" icon={ListChecks} label={t.nav.tasks} />

        {isAdmin && (
          <>
            <div className="mt-4 mb-1 px-3 text-[10px] font-semibold uppercase tracking-widest text-sidebar-foreground/40">
   {t.nav.admin}
  </div>
          <NavItem to="/audit-trail" icon={FileText} label={t.nav.auditTrail} />
         <NavItem to="/settings" icon={Settings} label={t.nav.settings} />
          {isGlobalAdmin && (
      <NavItem to="/admin/tenants" icon={Building2} label={t.nav.tenants ?? "Tenants"} />
 )}
           </>
        )}
      </nav>

      {/* User footer */}
      <div className="border-t border-sidebar-border px-3 py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
       {user?.firstName?.[0]}{user?.lastName?.[0]}
          </div>
 <div className="min-w-0 flex-1">
        <p className="text-xs font-medium text-sidebar-foreground truncate">{user?.fullName}</p>
    <p className="text-[10px] text-sidebar-foreground/50 truncate">{user?.role}</p>
   </div>
          <Button variant="ghost" size="icon" className="h-7 w-7 text-sidebar-foreground/50 hover:text-sidebar-foreground" onClick={logout} title={t.common.signOut}>
    <LogOut className="h-3.5 w-3.5" />
       </Button>
    </div>
 </div>
    </aside>
  );
}
