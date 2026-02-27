import { useState, useEffect, useCallback } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { auditLogsApi } from "@/services/api";
import type { AuditLogDto, AuditLogStats } from "@/services/api/types";
import { Loader2, Search, FileText, RefreshCw, Shield, ChevronDown, ChevronRight, AlertTriangle, Info, AlertCircle, Download } from "lucide-react";
import { format } from "date-fns";

const SEVERITY_CONFIG: Record<string, { icon: typeof Info; color: string; bg: string }> = {
  Info: { icon: Info, color: "text-blue-600", bg: "bg-blue-50 dark:bg-blue-950" },
  Warning: { icon: AlertTriangle, color: "text-amber-600", bg: "bg-amber-50 dark:bg-amber-950" },
  Critical: { icon: AlertCircle, color: "text-red-600", bg: "bg-red-50 dark:bg-red-950" },
};

const CATEGORY_COLORS: Record<string, string> = {
  Auth: "bg-violet-100 text-violet-700 dark:bg-violet-900 dark:text-violet-300",
  Case: "bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300",
  Document: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900 dark:text-emerald-300",
  Task: "bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300",
  Party: "bg-cyan-100 text-cyan-700 dark:bg-cyan-900 dark:text-cyan-300",
  Workflow: "bg-indigo-100 text-indigo-700 dark:bg-indigo-900 dark:text-indigo-300",
  Signing: "bg-rose-100 text-rose-700 dark:bg-rose-900 dark:text-rose-300",
  Meeting: "bg-teal-100 text-teal-700 dark:bg-teal-900 dark:text-teal-300",
  Settings: "bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-300",
  User: "bg-purple-100 text-purple-700 dark:bg-purple-900 dark:text-purple-300",
  System: "bg-gray-100 text-gray-700 dark:bg-gray-900 dark:text-gray-300",
};

// Human-readable labels for audit action codes
const ACTION_LABELS: Record<string, string> = {
  // Auth
  "Auth.Login": "User signed in",
  "Auth.Logout": "User signed out",
  "Auth.LoginFailed": "Failed sign-in attempt",
  "Auth.PasswordChanged": "Password changed",
  "Auth.PasswordReset": "Password reset",
  // Cases
  "Case.Created": "Case opened",
  "Case.Updated": "Case updated",
  "Case.Deleted": "Case deleted",
  "Case.StageAdvanced": "Case status changed",
  "Case.Closed": "Case closed",
  // Documents
  "Document.Uploaded": "Document uploaded",
  "Document.Reviewed": "Document reviewed",
  "Document.Deleted": "Document deleted",
  "Document.Signed": "Document digitally signed",
  // Tasks
  "Task.Created": "Task created",
  "Task.Updated": "Task updated",
  "Task.Completed": "Task completed",
  "Task.Deleted": "Task deleted",
  // Parties
  "Party.Added": "Party added to case",
  "Party.Updated": "Party details updated",
  "Party.Removed": "Party removed from case",
  // Users
  "User.Invited": "User invited",
  "User.Updated": "User profile updated",
  "User.Deactivated": "User deactivated",
  "User.PasswordAdminReset": "Admin reset user password",
  // Signing
  "Signing.KeyUploaded": "Signing certificate uploaded",
  "Signing.KeyDeactivated": "Signing certificate deactivated",
  "Signing.DocumentSigned": "Document digitally signed",
  // Settings
  "Settings.TenantUpdated": "Organisation settings saved",
  "Settings.FirmUpdated": "Firm details updated",
  "Settings.DeadlineSettingsUpdated": "Deadline settings updated",
// System / ONRC
  "ONRCFirmDatabase.Imported": "Firms database imported",
  "SystemData": "System data updated",
};

function friendlyAction(action: string): string {
  if (ACTION_LABELS[action]) return ACTION_LABELS[action];
  // Fallback: split on dot/camelCase
  const parts = action.split(".");
  if (parts.length === 2) return `${parts[0]}: ${parts[1].replace(/([A-Z])/g, " $1").trim()}`;
  return action.replace(/([A-Z])/g, " $1").trim();
}

function DetailRow({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null;
  return (
    <div className="flex gap-2 text-[11px]">
      <span className="text-muted-foreground font-medium min-w-[80px]">{label}:</span>
      <span className="text-foreground font-mono break-all">{value}</span>
    </div>
  );
}

function JsonBlock({ label, json }: { label: string; json: string | null | undefined }) {
  if (!json) return null;
  let formatted = json;
  try { formatted = JSON.stringify(JSON.parse(json), null, 2); } catch { /* keep raw */ }
  return (
    <div className="mt-1">
  <span className="text-[10px] text-muted-foreground font-semibold uppercase">{label}</span>
      <pre className="mt-0.5 rounded bg-muted/50 p-2 text-[10px] font-mono overflow-x-auto max-h-40">{formatted}</pre>
</div>
  );
}

function AuditRow({ log, t }: { log: AuditLogDto; t: any }) {
  const [expanded, setExpanded] = useState(false);
  const sev = SEVERITY_CONFIG[log.severity] ?? SEVERITY_CONFIG.Info;
  const SevIcon = sev.icon;
  const catColor = CATEGORY_COLORS[log.category] ?? CATEGORY_COLORS.System;
  const label = friendlyAction(log.action);
  const hasDetails = log.description || log.oldValues || log.newValues || log.changes ||
    log.requestMethod || log.requestPath || log.ipAddress || log.entityId;

  return (
    <div className={`px-4 py-2.5 ${expanded ? sev.bg : "hover:bg-muted/30"} transition-colors`}>
      <div className={`flex items-start gap-3 ${hasDetails ? "cursor-pointer" : ""}`} onClick={() => hasDetails && setExpanded(!expanded)}>
        <div className="mt-0.5 shrink-0 w-3.5">
   {hasDetails ? (expanded ? <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" /> : <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />) : null}
        </div>
        <SevIcon className={`h-4 w-4 mt-0.5 shrink-0 ${sev.color}`} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
    <Badge className={`text-[10px] px-1.5 py-0 ${catColor}`}>{log.category}</Badge>
       <span className="text-xs font-semibold text-foreground">{label}</span>
      {log.userEmail && <span className="text-[11px] text-muted-foreground">{log.userEmail}</span>}
          </div>
          {log.description && (
<p className="text-[11px] text-muted-foreground mt-0.5 truncate">{log.description}</p>
        )}
          {!log.description && log.entityType && (
  <p className="text-[10px] text-muted-foreground mt-0.5">
{log.entityType}{log.entityId ? ` — ${log.entityId.slice(0, 8)}…` : ""}
      </p>
  )}
     </div>
        <div className="text-right shrink-0">
          <p className="text-xs text-foreground">{format(new Date(log.timestamp), "dd MMM yyyy")}</p>
    <p className="text-[10px] text-muted-foreground">{format(new Date(log.timestamp), "HH:mm:ss")}</p>
        </div>
 {log.durationMs != null && (
       <span className="text-[10px] text-muted-foreground shrink-0 hidden lg:block">{log.durationMs}ms</span>
        )}
      </div>

      {expanded && (
   <div className="ml-10 mt-2 space-y-1 pb-1">
          {log.description && <DetailRow label="Description" value={log.description} />}
      <DetailRow label={t.audit.method ?? "Method"} value={log.requestMethod} />
          <DetailRow label={t.audit.path ?? "Path"} value={log.requestPath} />
          <DetailRow label={t.audit.status ?? "Status"} value={log.responseStatusCode?.toString()} />
   <DetailRow label={t.audit.duration ?? "Duration"} value={log.durationMs != null ? `${log.durationMs}ms` : null} />
          <DetailRow label="IP" value={log.ipAddress} />
<DetailRow label={t.audit.entity ?? "Entity"} value={log.entityId} />
          <DetailRow label="Correlation" value={log.correlationId} />
       <JsonBlock label={t.audit.oldValues ?? "Before"} json={log.oldValues} />
<JsonBlock label={t.audit.newValues ?? "After"} json={log.newValues} />
       <JsonBlock label={t.audit.changes ?? "Changes"} json={log.changes} />
        </div>
      )}
 </div>
  );
}

export default function AuditTrailPage() {
  const { isGlobalAdmin, isTenantAdmin } = useAuth();
  const { t } = useTranslation();
  const [logs, setLogs] = useState<AuditLogDto[]>([]);
  const [stats, setStats] = useState<AuditLogStats | null>(null);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
const [search, setSearch] = useState("");
  const [severity, setSeverity] = useState("");
  const [category, setCategory] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(0);
  const [pageSize] = useState(50);
  const [total, setTotal] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params: Record<string, string> = { pageSize: String(pageSize), page: String(page) };
      if (search) params.search = search;
      if (severity) params.severity = severity;
      if (category) params.category = category;
      if (fromDate) params.fromDate = fromDate;
      if (toDate) params.toDate = toDate;
      const [logsRes, statsRes] = await Promise.all([
        auditLogsApi.getAll(params),
        auditLogsApi.getStats({ from: fromDate || undefined, to: toDate || undefined }),
      ]);
      setLogs(logsRes.data.items);
      setTotal(logsRes.data.total);
      setStats(statsRes.data);
    } catch (err) { console.error(err); }
    finally { setLoading(false); }
  }, [search, severity, category, fromDate, toDate, page, pageSize]);

  useEffect(() => {
    setPage(0);
  }, [search, severity, category, fromDate, toDate]);

  useEffect(() => {
    auditLogsApi.getCategories().then(r => setCategories(r.data)).catch(() => {});
  }, []);

  useEffect(() => { load(); }, [load]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const handleDownload = async () => {
    try {
      const params: Record<string, string> = {};
      if (search) params.search = search;
      if (severity) params.severity = severity;
      if (category) params.category = category;
      if (fromDate) params.fromDate = fromDate;
      if (toDate) params.toDate = toDate;

      const response = await auditLogsApi.export(params);
      const blob = response.data;
      const url = window.URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `audit-logs-${format(new Date(), "yyyyMMdd-HHmmss")}.csv`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error(error);
    }
  };

  if (!isGlobalAdmin && !isTenantAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
        <Shield className="h-12 w-12 mb-3 opacity-30" />
      <p className="text-sm">{t.audit.noAccess}</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-foreground">{t.audit.title}</h1>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" className="gap-1.5 text-xs" onClick={handleDownload}>
            <Download className="h-3.5 w-3.5" />
            {t.common.export}
          </Button>
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={load}>
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Stats */}
      {stats && (
     <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="rounded-lg border border-border bg-card p-3">
     <p className="text-[10px] uppercase text-muted-foreground font-semibold">{t.audit.totalEvents ?? "Total Events"}</p>
        <p className="text-2xl font-bold text-foreground">{stats.total}</p>
          </div>
      {stats.bySeverity.map(s => {
            const cfg = SEVERITY_CONFIG[s.severity] ?? SEVERITY_CONFIG.Info;
   const Icon = cfg.icon;
    return (
     <div key={s.severity} className={`rounded-lg border border-border p-3 ${cfg.bg}`}>
      <div className="flex items-center gap-1.5">
        <Icon className={`h-3.5 w-3.5 ${cfg.color}`} />
       <p className="text-[10px] uppercase text-muted-foreground font-semibold">{s.severity}</p>
        </div>
     <p className="text-2xl font-bold text-foreground">{s.count}</p>
          </div>
            );
   })}
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-end">
        <div className="flex-1 min-w-[200px] relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
       <input
   type="text"
          placeholder={t.audit.searchPlaceholder ?? "Search actions, emails, paths..."}
    value={search}
            onChange={e => setSearch(e.target.value)}
    className="w-full rounded-lg border border-input bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
 />
        </div>
        <div>
          <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.audit.severity ?? "Severity"}</label>
<select
 value={severity}
            onChange={e => setSeverity(e.target.value)}
  className="rounded-md border border-input bg-background px-2.5 py-1.5 text-sm"
       >
 <option value="">{t.common?.all ?? "All"}</option>
        <option value="Info">Info</option>
 <option value="Warning">Warning</option>
    <option value="Critical">Critical</option>
   </select>
  </div>
        <div>
          <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.audit.category ?? "Category"}</label>
          <select
       value={category}
      onChange={e => setCategory(e.target.value)}
            className="rounded-md border border-input bg-background px-2.5 py-1.5 text-sm"
          >
            <option value="">{t.common?.all ?? "All"}</option>
            {categories.map(c => <option key={c} value={c}>{c}</option>)}
      </select>
   </div>
<div>
          <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.audit.from}</label>
          <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} className="rounded-md border border-input bg-background px-2.5 py-1.5 text-sm" />
        </div>
        <div>
          <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.audit.to}</label>
        <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} className="rounded-md border border-input bg-background px-2.5 py-1.5 text-sm" />
    </div>
      </div>

      {/* Results */}
   {loading ? (
        <div className="flex justify-center py-12"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>
 ) : (
      <>
        <div className="rounded-xl border border-border bg-card divide-y divide-border">
          {logs.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
              <FileText className="h-10 w-10 mb-2 opacity-30" />
   <p className="text-sm">{t.audit.noLogs}</p>
         </div>
          ) : logs.map(log => (
            <AuditRow key={log.id} log={log} t={t} />
          ))}
        </div>
        <div className="flex items-center justify-between px-1">
          <p className="text-xs text-muted-foreground">{total} {t.common.records}</p>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" className="h-7 text-xs" disabled={page <= 0} onClick={() => setPage(p => Math.max(0, p - 1))}>
              Prev
            </Button>
            <span className="text-xs text-muted-foreground">{page + 1} / {totalPages}</span>
            <Button variant="outline" size="sm" className="h-7 text-xs" disabled={page + 1 >= totalPages} onClick={() => setPage(p => p + 1)}>
              Next
            </Button>
          </div>
        </div>
      </>
      )}
    </div>
  );
}
