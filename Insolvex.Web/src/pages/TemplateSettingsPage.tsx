import { useState, useEffect, useRef } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { settingsApi, type TemplateUploadResult } from "@/services/api/settingsApi";
import type { TemplateInfo } from "@/services/api/workflow";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  FileText, Upload, Download, Trash2, Loader2,
  CheckCircle2, AlertTriangle, Globe, Building2, HardDrive, RefreshCw,
} from "lucide-react";
import { format } from "date-fns";

const TEMPLATE_LABELS: Record<string, string> = {
  CourtOpeningDecision: "Court Opening Decision",
  CreditorNotificationBpi: "Creditor Notification (BPI) — DOCX",
  CreditorNotificationHtml: "Notificare Deschidere Procedură — HTML→PDF",
  ReportArt97: "Report Art. 97 (40 zile)",
  PreliminaryClaimsTable: "Preliminary Claims Table",
  CreditorsMeetingMinutes: "Creditors Meeting Minutes",
  DefinitiveClaimsTable: "Definitive Claims Table",
  FinalReportArt167: "Final Report Art. 167",
};

function sourceLabel(src: string) {
  switch (src) {
    case "tenant": return { text: "Tenant Override", icon: Building2, color: "text-blue-500", badge: "default" as const };
    case "global-db": return { text: "Global (DB)", icon: Globe, color: "text-green-500", badge: "success" as const };
    case "disk": return { text: "Disk File", icon: HardDrive, color: "text-muted-foreground", badge: "secondary" as const };
    default: return { text: "Missing", icon: AlertTriangle, color: "text-destructive", badge: "destructive" as const };
  }
}

function formatBytes(bytes: number) {
  if (bytes === 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function TemplateSettingsPage() {
  const { isGlobalAdmin } = useAuth();
  const [templates, setTemplates] = useState<TemplateInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploadingType, setUploadingType] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<TemplateUploadResult | null>(null);
  const [uploadAsGlobal, setUploadAsGlobal] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const pendingTypeRef = useRef<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await settingsApi.templates.getAll();
      setTemplates(r.data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const triggerUpload = (templateType: string, asGlobal = false) => {
    pendingTypeRef.current = templateType;
    setUploadAsGlobal(asGlobal);
    fileInputRef.current?.click();
  };

  const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const templateType = pendingTypeRef.current;
    if (!file || !templateType) return;
    e.target.value = "";

    setUploadingType(templateType);
    try {
      const r = await settingsApi.templates.upload(file, templateType, {
        name: TEMPLATE_LABELS[templateType] ?? templateType,
        global: uploadAsGlobal,
      });
      setLastResult(r.data);
      await load();
    } catch (err) {
      console.error(err);
    } finally {
      setUploadingType(null);
    }
  };

  const handleDelete = async (id: string) => {
    setDeletingId(id);
    try {
      await settingsApi.templates.delete(id);
      await load();
    } catch (err) {
      console.error(err);
    } finally {
      setDeletingId(null);
    }
  };

  const downloadFile = async (id: string, fileName: string) => {
    const token = localStorage.getItem("authToken");
    const tenantId = localStorage.getItem("selectedTenantId");
    const url = settingsApi.templates.downloadUrl(id);
    const res = await fetch(url, {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
      },
    });
    if (!res.ok) return;
    const blob = await res.blob();
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="h-7 w-7 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-foreground">Document Templates</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Mail-merge templates used to generate insolvency documents.
            {isGlobalAdmin && " Global admins can replace disk files; tenant admins upload per-tenant overrides."}
          </p>
        </div>
        <Button variant="ghost" size="sm" onClick={load} className="gap-1.5 text-xs">
          <RefreshCw className="h-3.5 w-3.5" /> Refresh
        </Button>
      </div>

      {/* Legend */}
      <div className="flex flex-wrap gap-3 rounded-lg border border-border bg-card/50 px-4 py-2.5">
        <span className="text-xs text-muted-foreground font-medium">Effective source:</span>
        {(["tenant", "global-db", "disk", "missing"] as const).map(src => {
          const s = sourceLabel(src);
          return (
            <span key={src} className={`flex items-center gap-1 text-xs ${s.color}`}>
              <s.icon className="h-3.5 w-3.5" /> {s.text}
            </span>
          );
        })}
      </div>

      {lastResult && (
        <div className="rounded-lg border border-green-200 bg-green-50 dark:bg-green-950/20 dark:border-green-800 px-4 py-2.5 flex items-center gap-2">
          <CheckCircle2 className="h-4 w-4 text-green-600 shrink-0" />
          <p className="text-sm text-green-700 dark:text-green-300">
            Uploaded <strong>{lastResult.fileName}</strong> as {lastResult.isGlobal ? "global" : "tenant"} override (v{lastResult.version})
          </p>
          <button onClick={() => setLastResult(null)} className="ml-auto text-green-600 hover:text-green-800">×</button>
        </div>
      )}

      {/* Template list */}
      <div className="rounded-xl border border-border bg-card overflow-hidden divide-y divide-border">
        {templates.length === 0 && (
          <div className="px-6 py-10 text-center">
            <FileText className="h-10 w-10 mx-auto text-muted-foreground/40 mb-3" />
            <p className="text-sm text-muted-foreground">No templates configured.</p>
          </div>
        )}

        {templates.map(tpl => {
          const src = sourceLabel(tpl.effectiveSource);
          const isUploading = uploadingType === tpl.templateType;
          const displayName = TEMPLATE_LABELS[tpl.templateType] ?? tpl.templateType;
          const effectiveFile = tpl.tenantOverrideFileName ?? tpl.globalOverrideFileName ?? tpl.defaultFileName;
          const effectiveSize = tpl.tenantOverrideFileSizeBytes || tpl.globalOverrideFileSizeBytes || tpl.diskFileSizeBytes;
          const overrideId = tpl.tenantOverrideId ?? tpl.globalOverrideId;

          return (
            <div key={tpl.templateType} className="px-4 py-3">
              <div className="flex items-start gap-3">
                {/* Icon */}
                <div className={`mt-0.5 shrink-0 ${src.color}`}>
                  <src.icon className="h-5 w-5" />
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="text-sm font-medium text-foreground">{displayName}</p>
                    <Badge variant={src.badge} className="text-[10px]">{src.text}</Badge>
                    {tpl.tenantOverrideId && (
                      <Badge variant="outline" className="text-[10px] text-blue-500 border-blue-300">
                        v{tpl.tenantOverrideVersion}
                      </Badge>
                    )}
                    {!tpl.tenantOverrideId && tpl.globalOverrideId && (
                      <Badge variant="outline" className="text-[10px] text-green-600 border-green-300">
                        v{tpl.globalOverrideVersion}
                      </Badge>
                    )}
                  </div>
                  <p className="text-[11px] text-muted-foreground mt-0.5 truncate">
                    {effectiveFile} · {formatBytes(effectiveSize)}
                  </p>

                  {/* Disk file status */}
                  {tpl.diskExists ? (
                    <p className="text-[10px] text-muted-foreground/60 mt-0.5">
                      Disk: {tpl.defaultFileName} ({formatBytes(tpl.diskFileSizeBytes)})
                    </p>
                  ) : (
                    <p className="text-[10px] text-amber-500 mt-0.5">
                      ⚠ Disk file not found — upload a template to enable generation
                    </p>
                  )}

                  {/* Override info */}
                  {tpl.tenantOverrideId && (
                    <p className="text-[10px] text-blue-500 mt-0.5">
                      Tenant override: {tpl.tenantOverrideFileName} ({formatBytes(tpl.tenantOverrideFileSizeBytes)})
                    </p>
                  )}
                  {tpl.globalOverrideId && !tpl.tenantOverrideId && (
                    <p className="text-[10px] text-green-600 mt-0.5">
                      Global override: {tpl.globalOverrideFileName} ({formatBytes(tpl.globalOverrideFileSizeBytes)})
                    </p>
                  )}
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1 shrink-0">
                  {/* Download effective template */}
                  {overrideId && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 px-2 text-xs gap-1"
                      onClick={() => downloadFile(overrideId, effectiveFile)}
                      title="Download current effective template"
                    >
                      <Download className="h-3.5 w-3.5" />
                    </Button>
                  )}

                  {/* Upload tenant override */}
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-8 px-2.5 text-xs gap-1"
                    onClick={() => triggerUpload(tpl.templateType, false)}
                    disabled={isUploading}
                    title="Upload tenant-specific override"
                  >
                    {isUploading ? (
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Upload className="h-3.5 w-3.5" />
                    )}
                    {tpl.tenantOverrideId ? "Replace Override" : "Upload Override"}
                  </Button>

                  {/* Global upload (GlobalAdmin only) */}
                  {isGlobalAdmin && (
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-8 px-2.5 text-xs gap-1 border-green-300 text-green-700 hover:bg-green-50 dark:text-green-400 dark:border-green-700"
                      onClick={() => triggerUpload(tpl.templateType, true)}
                      disabled={isUploading}
                      title="Upload as global default"
                    >
                      <Globe className="h-3.5 w-3.5" />
                      Global
                    </Button>
                  )}

                  {/* Delete override */}
                  {tpl.tenantOverrideId && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 px-2 text-xs text-destructive hover:text-destructive hover:bg-destructive/10"
                      onClick={() => handleDelete(tpl.tenantOverrideId!)}
                      disabled={deletingId === tpl.tenantOverrideId}
                      title="Remove tenant override (revert to global/disk)"
                    >
                      {deletingId === tpl.tenantOverrideId ? (
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Trash2 className="h-3.5 w-3.5" />
                      )}
                    </Button>
                  )}
                  {isGlobalAdmin && tpl.globalOverrideId && !tpl.tenantOverrideId && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 px-2 text-xs text-destructive hover:text-destructive hover:bg-destructive/10"
                      onClick={() => handleDelete(tpl.globalOverrideId!)}
                      disabled={deletingId === tpl.globalOverrideId}
                      title="Remove global DB override (revert to disk file)"
                    >
                      {deletingId === tpl.globalOverrideId ? (
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Trash2 className="h-3.5 w-3.5" />
                      )}
                    </Button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".doc,.docx,.pdf"
        className="hidden"
        onChange={handleFileSelected}
      />

      {/* Info panel */}
      <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 space-y-1">
        <p className="text-xs font-medium text-foreground">Resolution order</p>
        <ol className="text-xs text-muted-foreground space-y-0.5 list-decimal list-inside">
          <li><span className="text-blue-500 font-medium">Tenant override</span> — uploaded by your tenant admin, specific to your workspace</li>
          <li><span className="text-green-600 font-medium">Global DB override</span> — uploaded by a GlobalAdmin, applies to all tenants without their own override</li>
          <li><span className="text-muted-foreground font-medium">Disk file</span> — shipped with the application in <code className="text-[10px] bg-muted px-1 rounded">Templates-Ro/</code></li>
        </ol>
      </div>
    </div>
  );
}
