import { useState, useEffect, useCallback } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTenant } from "@/contexts/TenantContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { tenantsApi } from "@/services/api";
import {
  Loader2, Building2, Shield, Plus, X, Users, Briefcase, Building,
  RefreshCw, Pencil,
} from "lucide-react";
import { format } from "date-fns";

interface TenantRow {
  id: string;
  name: string;
  domain: string | null;
  isActive: boolean;
  isDemo: boolean;
  planName: string | null;
  region: string;
  subscriptionExpiry: string | null;
  createdOn: string | null;
  userCount: number;
  companyCount: number;
  caseCount: number;
}

function formatSafeDate(value: string | null | undefined): string | null {
  if (!value) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  return format(parsed, "dd MMM yyyy");
}

function EditTenantModal({
  tenant,
  onClose,
  onSaved,
}: {
  tenant: TenantRow | null; // null = create mode
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(tenant?.name ?? "");
  const [domain, setDomain] = useState(tenant?.domain ?? "");
  const [planName, setPlanName] = useState(tenant?.planName ?? "Free");
  const [region, setRegion] = useState(tenant?.region ?? "Romania");
  const [isActive, setIsActive] = useState(tenant?.isActive ?? true);
  const [isDemo, setIsDemo] = useState(tenant?.isDemo ?? false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const handleSave = async () => {
setSaving(true);
    setError("");
    try {
      if (tenant) {
   await tenantsApi.update(tenant.id, { name, domain: domain || undefined, planName, isActive, isDemo, region });
 } else {
        await tenantsApi.create({ name, domain: domain || undefined, planName, region, isDemo });
     }
      onSaved();
    } catch (err: unknown) {
    const axErr = err as { response?: { data?: { message?: string } } };
  setError(axErr?.response?.data?.message || "Failed to save tenant");
 } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4 p-5 space-y-4"
   onClick={(e) => e.stopPropagation()}
      >
     <div className="flex items-center justify-between">
 <h2 className="text-sm font-semibold text-foreground">
       {tenant ? (t.tenants?.editTenant ?? "Edit Tenant") : (t.tenants?.createTenant ?? "Create Tenant")}
    </h2>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onClose}>
  <X className="h-4 w-4" />
   </Button>
        </div>

        <div className="space-y-3">
   <div>
    <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">
              {t.tenants?.name ?? "Name"}
    </label>
     <input
              value={name}
  onChange={(e) => setName(e.target.value)}
      className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
   placeholder="Acme Insolvency Ltd"
    />
     </div>
          <div>
     <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">
        {t.tenants?.domain ?? "Domain"}
 </label>
            <input
     value={domain}
         onChange={(e) => setDomain(e.target.value)}
       className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
      placeholder="acme.insolvio.com"
            />
</div>
          <div>
      <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">
 {t.tenants?.plan ?? "Plan"}
            </label>
        <select
         value={planName}
         onChange={(e) => setPlanName(e.target.value)}
        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
     >
   <option value="Free">Free</option>
   <option value="Professional">Professional</option>
       <option value="Enterprise">Enterprise</option>
   </select>
       </div>
          <div>
     <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">
              {t.tenants?.region ?? "Region"}
  </label>
            <select
  value={region}
              onChange={(e) => setRegion(e.target.value)}
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          >
         <option value="Romania">🇷🇴 România</option>
      <option value="Hungary">🇭🇺 Hungary</option>
  </select>
  </div>
          {tenant && (
            <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
  <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
         {t.common.active}
         </label>
        )}
        {tenant && (
            <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
    <input type="checkbox" checked={isDemo} onChange={(e) => setIsDemo(e.target.checked)} />
              Demo tenant (enables Demo Reset feature)
            </label>
         )}
        </div>

    {error && <p className="text-xs text-destructive">{error}</p>}

   <div className="flex justify-end gap-2">
      <Button variant="outline" size="sm" onClick={onClose}>
    {t.common.cancel}
          </Button>
          <Button size="sm" onClick={handleSave} disabled={saving || !name.trim()}>
  {saving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
      {tenant ? t.common.save : t.common.create}
          </Button>
    </div>
      </div>
    </div>
  );
}

export default function TenantAdminPage() {
  const { isGlobalAdmin } = useAuth();
  const { refreshTenants } = useTenant();
  const { t } = useTranslation();
  const [tenants, setTenants] = useState<TenantRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingTenant, setEditingTenant] = useState<TenantRow | null | undefined>(undefined); // undefined = closed

  const load = useCallback(async () => {
    setLoading(true);
    try {
const r = await tenantsApi.getAll();
setTenants(r.data as unknown as TenantRow[]);
    } catch (err) {
 console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  if (!isGlobalAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
        <Shield className="h-12 w-12 mb-3 opacity-30" />
<p className="text-sm">{t.tenants?.noAccess ?? "Only Global Admins can manage tenants."}</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-foreground">{t.tenants?.title ?? "Tenant Administration"}</h1>
 <div className="flex gap-2">
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={load}>
      <RefreshCw className="h-4 w-4" />
        </Button>
          <Button size="sm" className="gap-1" onClick={() => setEditingTenant(null)}>
         <Plus className="h-3.5 w-3.5" />
     {t.tenants?.createTenant ?? "Create Tenant"}
    </Button>
  </div>
    </div>

      {loading ? (
        <div className="flex justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
   </div>
    ) : (
  <div className="rounded-xl border border-border bg-card divide-y divide-border">
          {tenants.length === 0 ? (
            <p className="px-4 py-8 text-center text-sm text-muted-foreground">
{t.tenants?.noTenants ?? "No tenants yet."}
</p>
          ) : (
            tenants.map((tenant) => (
          <div
      key={tenant.id}
                className="flex items-center gap-4 px-4 py-3 hover:bg-muted/30 transition-colors"
   >
         <Building2
 className={`h-5 w-5 shrink-0 ${tenant.isActive ? "text-primary" : "text-muted-foreground"}`}
    />
     <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
 <p className="text-sm font-medium text-foreground truncate">{tenant.name}</p>
     <Badge variant={tenant.isActive ? "success" : "destructive"} className="text-[10px]">
      {tenant.isActive ? t.common.active : t.common.inactive}
            </Badge>
       {tenant.planName && (
         <Badge variant="outline" className="text-[10px]">
     {tenant.planName}
          </Badge>
       )}
    {tenant.isDemo && (
     <Badge variant="secondary" className="text-[10px]">
     Demo
   </Badge>
            )}
    <Badge variant="outline" className="text-[10px]">
{tenant.region === "Romania" ? "🇷🇴" : "🇭🇺"} {tenant.region}
         </Badge>
       </div>
    <p className="text-xs text-muted-foreground">
      {tenant.domain || "No domain"}
      {formatSafeDate(tenant.createdOn) ? ` · Created ${formatSafeDate(tenant.createdOn)}` : ""}
      {formatSafeDate(tenant.subscriptionExpiry) ? ` · Expires ${formatSafeDate(tenant.subscriptionExpiry)}` : ""}
    </p>
              </div>

         <div className="flex items-center gap-3 text-xs text-muted-foreground shrink-0">
      <span className="flex items-center gap-1" title="Users">
   <Users className="h-3.5 w-3.5" />
        {tenant.userCount}
 </span>
<span className="flex items-center gap-1" title="Companies">
   <Building className="h-3.5 w-3.5" />
        {tenant.companyCount}
       </span>
        <span className="flex items-center gap-1" title="Cases">
      <Briefcase className="h-3.5 w-3.5" />
            {tenant.caseCount}
         </span>
    </div>

 <Button
     variant="ghost"
 size="icon"
           className="h-8 w-8 shrink-0"
        onClick={() => setEditingTenant(tenant)}
      >
          <Pencil className="h-3.5 w-3.5" />
    </Button>
     </div>
            ))
          )}
   </div>
      )}

      {/* Edit / Create Modal */}
 {editingTenant !== undefined && (
 <EditTenantModal
          tenant={editingTenant}
          onClose={() => setEditingTenant(undefined)}
    onSaved={() => {
      setEditingTenant(undefined);
load();
       refreshTenants(); // Update the tenant selector in sidebar
       }}
        />
      )}
    </div>
  );
}
