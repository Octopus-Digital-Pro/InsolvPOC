import { useState, useEffect, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { auditLogsApi, casesApi, companiesApi } from "@/services/api";
import { onrcApi } from "@/services/api/onrc";
import type { ONRCFirmResult } from "@/services/api/onrc";
import { workflowApi } from "@/services/api/workflow";
import { documentTemplatesApi } from "@/services/api/documentTemplatesApi";
import { caseEmailsApi } from "@/services/api/caseWorkspace";
import { tasksApi } from "@/services/api/tasks";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CaseDto, CasePartyDto, DocumentDto, TaskDto, CompanyDto } from "@/services/api/types";
import type { CaseEmailDto } from "@/services/api/caseWorkspace";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import CreditorMeetingModal from "@/components/CreditorMeetingModal";
import DocumentSigningPanel from "@/components/DocumentSigningPanel";
import CaseTasksTab from "@/components/CaseTasksTab";
import CaseEmailsTab from "@/components/CaseEmailsTab";
import CaseWorkflowPanel from "@/components/CaseWorkflowPanel";
import { downloadAuthFile } from "@/utils/downloadAuthFile";
import {
  Loader2, FileText, Upload, Users,
  Brain, CalendarDays, RefreshCw, Layers,
  ListChecks, Mail, Download, FileOutput,
  History, Plus, Search, X, Building2,
} from "lucide-react";
import { format } from "date-fns";

function InfoRow({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null;
  return (
    <div>
      <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="text-sm text-foreground">{value}</p>
    </div>
  );
}

function formatMoney(val: number | null | undefined): string | null {
  if (val == null) return null;
  return val.toLocaleString("ro-RO", { style: "currency", currency: "RON", maximumFractionDigits: 0 });
}

export default function CaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, locale } = useTranslation();
  const [caseData, setCaseData] = useState<CaseDto | null>(null);
  const [parties, setParties] = useState<CasePartyDto[]>([]);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [meetingOpen, setMeetingOpen] = useState(false);
  const [summary, setSummary] = useState<Record<string, unknown> | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"overview" | "workflow" | "tasks" | "docs" | "parties" | "emails" | "calendar" | "templates" | "activity">("overview");
  const [caseTasks, setCaseTasks] = useState<TaskDto[]>([]);
  const [caseEmails, setCaseEmails] = useState<CaseEmailDto[]>([]);
  const [docUploading, setDocUploading] = useState(false);
  const docUploadRef = useRef<HTMLInputElement>(null);
  const [addPartyOpen, setAddPartyOpen] = useState(false);

  const load = useCallback(() => {
    if (!id) return;
    Promise.all([
      casesApi.getById(id),
      casesApi.getParties(id),
      casesApi.getDocuments(id),
      tasksApi.getAll({ companyId: undefined, myTasks: false }).then(r => ({
        data: r.data.filter((t: TaskDto) => (t as unknown as Record<string, unknown>).caseId === id)
      })).catch(() => ({ data: [] as TaskDto[] })),
      caseEmailsApi.getByCaseId(id).catch(() => ({ data: [] as CaseEmailDto[] })),
    ]).then(([caseRes, partiesRes, docsRes, tasksRes, emailsRes]) => {
      setCaseData(caseRes.data);
      setParties(partiesRes.data);
      setDocuments(docsRes.data);
      setCaseTasks(tasksRes.data);
      setCaseEmails(emailsRes.data);
    }).catch(console.error)
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => { load(); }, [id]);

  const loadSummary = async () => {
    if (!id) return;
    setSummaryLoading(true);
    try {
      const r = await workflowApi.getLatestSummary(id);
      if (r.data.exists) setSummary(r.data);
    } catch (e) { console.error(e); }
    finally { setSummaryLoading(false); }
  };

  const generateSummary = async () => {
    if (!id) return;
    setSummaryLoading(true);
    try {
      const r = await workflowApi.generateSummary(id, "manual");
      setSummary({ exists: true, ...r.data });
    } catch (e) { console.error(e); }
    finally { setSummaryLoading(false); }
  };

  useEffect(() => { loadSummary(); }, [id]);

  const handleDocUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !id) return;
    e.target.value = "";
    setDocUploading(true);
    try {
      const token = localStorage.getItem("authToken");
      const formData = new FormData();
      formData.append("file", file);
      const res = await fetch("/api/documents/upload", {
        method: "POST",
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        body: formData,
      });
      if (res.ok) {
        const data = await res.json();
        navigate(`/documents/${data.id}/review?caseId=${id}`);
      }
    } catch (err) { console.error(err); }
    finally { setDocUploading(false); }
  };

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!caseData) return <p className="p-8 text-muted-foreground">{t.cases.noCases}</p>;

  const statusLabel = (s: string): string =>
    (t.statuses as Record<string, string>)?.[s] ?? s;

  const partyRoleLabel = (role: string): string => {
    const key = role.charAt(0).toLowerCase() + role.slice(1);
    return (t.partyRoles as Record<string, string>)[key] ?? role.replace(/([A-Z])/g, " $1").trim();
  };

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <BackButton className="cursor-pointer flex items-center gap-2 mb-2" onClick={() => navigate("/cases")}>{t.cases.backToCases}</BackButton>

      {/* Case header */}
      <div className="rounded-xl border border-border bg-card p-5">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-bold text-card-foreground">{t.cases.caseNumber} {caseData.caseNumber}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{caseData.debtorName}</p>
          </div>
          <Badge variant="secondary">{statusLabel(caseData.status)}</Badge>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-x-8 gap-y-3 sm:grid-cols-3">
          <InfoRow label={t.cases.court} value={caseData.courtName} />
          <InfoRow label={t.cases.courtSection} value={caseData.courtSection} />
          <InfoRow label={t.cases.judgeSyndic} value={caseData.judgeSyndic} />
          <InfoRow label={t.cases.procedureType} value={caseData.procedureType} />
          <InfoRow label={t.cases.lawReference} value={caseData.lawReference} />
          <InfoRow label={t.cases.debtorCui} value={caseData.debtorCui} />
          <InfoRow label={t.cases.practitioner} value={caseData.practitionerName} />
          <InfoRow label={t.cases.practitionerRole} value={caseData.practitionerRole} />
          <InfoRow label={t.cases.company} value={caseData.companyName} />
          <InfoRow label={t.cases.assignedTo} value={caseData.assignedToName} />
          {caseData.openingDate && <InfoRow label={t.cases.openingDate} value={format(new Date(caseData.openingDate), "dd MMM yyyy")} />}
          {caseData.nextHearingDate && <InfoRow label={t.cases.nextHearing} value={format(new Date(caseData.nextHearingDate), "dd MMM yyyy")} />}
          {caseData.claimsDeadline && <InfoRow label={t.cases.claimsDeadline} value={format(new Date(caseData.claimsDeadline), "dd MMM yyyy")} />}
          <InfoRow label="BPI" value={caseData.bpiPublicationNo} />
          {caseData.openingDecisionNo && <InfoRow label="Opening Decision" value={caseData.openingDecisionNo} />}
        </div>

        {/* Financial summary */}
        {caseData.totalClaimsRon != null && (
          <div className="mt-4 pt-4 border-t border-border grid grid-cols-2 gap-x-8 gap-y-2 sm:grid-cols-3 text-sm">
            <InfoRow label="Total Claims" value={formatMoney(caseData.totalClaimsRon)} />
            <InfoRow label="Secured" value={formatMoney(caseData.securedClaimsRon)} />
            <InfoRow label="Unsecured" value={formatMoney(caseData.unsecuredClaimsRon)} />
            <InfoRow label="Budgetary" value={formatMoney(caseData.budgetaryClaimsRon)} />
            <InfoRow label="Employee" value={formatMoney(caseData.employeeClaimsRon)} />
            <InfoRow label="Est. Assets" value={formatMoney(caseData.estimatedAssetValueRon)} />
          </div>
        )}
      </div>

      {/* Workspace layout */}
      <div className="space-y-3">
        {/* Actions */}
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
            onClick={() => setMeetingOpen(true)}
          >
            <Users className="h-3.5 w-3.5" />
            Call Creditor Meeting
          </Button>
        </div>

        {/* Main panel with tabs */}
        <div className="space-y-3">
          {/* Tab bar */}
          <div className="flex gap-1 rounded-lg border border-border bg-card p-1 overflow-x-auto">
            {([
              { id: "overview" as const, label: "Overview", icon: Brain },
              { id: "workflow" as const, label: "Workflow", icon: Layers },
              { id: "tasks" as const, label: `Tasks (${caseTasks.length})`, icon: ListChecks },
              { id: "docs" as const, label: t.cases.documents, icon: FileText },
              { id: "parties" as const, label: "Parties", icon: Users },
              { id: "emails" as const, label: `Emails (${caseEmails.length})`, icon: Mail },
              { id: "calendar" as const, label: "Calendar", icon: CalendarDays },
              { id: "templates" as const, label: "Templates", icon: FileOutput },
              { id: "activity" as const, label: "Activity", icon: History },
            ]).map(tb => (
              <button
                key={tb.id}
                onClick={() => setActiveTab(tb.id)}
                className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors whitespace-nowrap ${
                  activeTab === tb.id
                    ? "bg-primary text-primary-foreground"
                    : "text-muted-foreground hover:bg-accent hover:text-foreground"
                }`}
              >
                <tb.icon className="h-3.5 w-3.5" />
                {tb.label}
              </button>
            ))}
          </div>

          {/* Overview Tab */}
          {activeTab === "overview" && (
            <div className="space-y-4">
              {/* AI Summary */}
              <div className="rounded-xl border border-border bg-card p-4">
                <div className="flex items-center justify-between mb-3">
                  <h3 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    <Brain className="h-3.5 w-3.5" /> AI Case Summary
                  </h3>
                  <Button variant="ghost" size="sm" className="text-xs gap-1 h-7" onClick={generateSummary} disabled={summaryLoading}>
                    {summaryLoading ? <Loader2 className="h-3 w-3 animate-spin" /> : <RefreshCw className="h-3 w-3" />}
                    {summary ? "Refresh" : "Generate"}
                  </Button>
                </div>
                {summary ? (
                  <div className="prose prose-sm max-w-none text-sm text-foreground [&_h2]:text-base [&_h2]:font-semibold [&_li]:text-sm">
                    <div dangerouslySetInnerHTML={{
                      __html: (summary.text as string || "").replace(/\n/g, "<br>").replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
                    }} />
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">No summary generated yet. Click "Generate" to create an AI summary.</p>
                )}
              </div>
            </div>
          )}

          {/* Workflow Tab */}
          {activeTab === "workflow" && (
            <CaseWorkflowPanel caseId={id!} />
          )}

          {/* Tasks Tab */}
          {activeTab === "tasks" && (
            <CaseTasksTab caseId={id!} tasks={caseTasks} onRefresh={load} />
          )}

          {/* Documents Tab */}
          {activeTab === "docs" && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <FileText className="h-3.5 w-3.5" /> {t.cases.documents} ({documents.length})
                </h2>
                <div className="flex gap-1.5">
                  {documents.length > 0 && (
                    <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
                      onClick={() => downloadAuthFile(casesApi.downloadZipUrl(id!), `case_${caseData.caseNumber.replace(/\//g, "-")}_docs.zip`)}>
                      <Download className="h-3.5 w-3.5" />ZIP
                    </Button>
                  )}
                  <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
                    onClick={() => docUploadRef.current?.click()}
                    disabled={docUploading}>
                    {docUploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />}
                    {t.cases.uploadDocument}
                  </Button>
                  <input ref={docUploadRef} type="file" accept=".pdf,.doc,.docx,image/*" className="hidden" onChange={handleDocUpload} />
                </div>
              </div>
              <div className="rounded-xl border border-border bg-card divide-y divide-border">
                {documents.length === 0 ? (
                  <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.cases.noDocuments}</p>
                ) : (
                  documents.map(d => (
                    <div key={d.id} className="px-4 py-3 space-y-2">
                      <div className="flex items-center gap-3">
                        <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-medium text-foreground truncate">{d.sourceFileName}</p>
                          <p className="text-xs text-muted-foreground">{d.docType} · {d.uploadedBy}</p>
                        </div>
                        <span className="text-[10px] text-muted-foreground shrink-0">{format(new Date(d.uploadedAt), "dd MMM yyyy")}</span>
                      </div>
                      <DocumentSigningPanel
                        documentId={d.id}
                        fileName={d.sourceFileName}
                        requiresSignature={(d as unknown as Record<string, unknown>).requiresSignature as boolean | undefined}
                        isSigned={(d as unknown as Record<string, unknown>).isSigned as boolean | undefined}
                      />
                    </div>
                  ))
     )}
              </div>
            </div>
          )}

          {/* Parties Tab */}
          {activeTab === "parties" && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <Users className="h-3.5 w-3.5" /> Parties ({parties.length})
                </h2>
                <Button size="sm" className="gap-1.5 text-xs h-7" onClick={() => setAddPartyOpen(true)}>
                  <Plus className="h-3.5 w-3.5" /> Add Party
                </Button>
              </div>
              <div className="rounded-xl border border-bordered bg-card divide-y divide-border">
                {parties.length === 0 ? (
                  <p className="px-4 py-6 text-center text-sm text-muted-foreground">No parties added yet.</p>
                ) : (
                  parties.map(p => (
                    <div key={p.id} className="flex items-center gap-3 px-4 py-2.5">
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-foreground truncate">{p.companyName}</p>
                        {p.roleDescription && <p className="text-[10px] text-muted-foreground">{p.roleDescription}</p>}
                      </div>
                      <Badge variant="outline" className="text-[10px] shrink-0">{partyRoleLabel(p.role)}</Badge>
                      {p.claimAmountRon != null && (
                        <span className="text-xs font-medium text-foreground shrink-0">{formatMoney(p.claimAmountRon)}</span>
                      )}
                      {p.claimAccepted != null && (
                        <Badge variant={p.claimAccepted ? "success" : "warning"} className="text-[10px] shrink-0">
                          {p.claimAccepted ? "Accepted" : "Pending"}
                        </Badge>
                      )}
                    </div>
                  ))
    )}
              </div>
            </div>
          )}

          {/* Emails Tab */}
          {activeTab === "emails" && (
            <CaseEmailsTab caseId={id!} emails={caseEmails} onRefresh={load} />
          )}

          {/* Calendar Tab */}
          {activeTab === "calendar" && (
            <CalendarTab caseId={id!} />
          )}

          {/* Templates Tab */}
          {activeTab === "templates" && (
            <TemplatesTab caseId={id!} />
          )}

          {/* Activity Timeline Tab */}
          {activeTab === "activity" && (
            <CaseAuditActivity caseId={id!} caseNumber={caseData.caseNumber} locale={locale} />
          )}
        </div>
      </div>

      {/* Add Party Modal */}
      {addPartyOpen && (
        <AddPartyModal
          caseId={id!}
          locale={locale}
          onAdded={() => { load(); setAddPartyOpen(false); }}
          onClose={() => setAddPartyOpen(false)}
        />
      )}

      {/* Creditor Meeting Modal */}
      <CreditorMeetingModal
        caseId={id!}
        open={meetingOpen}
        onClose={() => setMeetingOpen(false)}
        onCreated={load}
      />
    </div>
  );
}

/* ── Calendar Tab Component ─────────────────────────── */
function CalendarTab({ caseId }: { caseId: string }) {
  const [events, setEvents] = useState<Array<Record<string, unknown>>>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    workflowApi.getCaseCalendar(caseId)
      .then(r => setEvents(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [caseId]);

  if (loading) return <div className="p-6 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" /></div>;

  return (
    <div className="rounded-xl border border-border bg-card divide-y divide-border">
      {events.length === 0 ? (
        <p className="px-4 py-6 text-center text-sm text-muted-foreground">No calendar events yet. Schedule a creditor meeting or add deadlines.</p>
      ) : (
        events.map(e => (
          <div key={e.id as string} className="flex items-center gap-3 px-4 py-2.5">
            <CalendarDays className={`h-4 w-4 shrink-0 ${(e.eventType as string) === "Meeting" ? "text-primary" : "text-muted-foreground"}`} />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-foreground truncate">{e.title as string}</p>
              {!!e.location && <p className="text-[10px] text-muted-foreground">{String(e.location)}</p>}
            </div>
            <Badge variant="outline" className="text-[10px] shrink-0">{e.eventType as string}</Badge>
            <span className="text-[10px] text-muted-foreground shrink-0">
              {format(new Date(e.start as string), "dd MMM yyyy HH:mm")}
            </span>
          </div>
        ))
      )}
    </div>
  );
}

function CaseAuditActivity({ caseId, caseNumber, locale }: { caseId: string; caseNumber: string; locale: "en" | "ro" | "hu" }) {
  const [logs, setLogs] = useState<Array<import("@/services/api/types").AuditLogDto>>([]);
  const [loading, setLoading] = useState(true);

  const texts = {
    en: {
      title: "Case activity",
      empty: "No related audit activity found.",
    },
    ro: {
      title: "Activitate dosar",
      empty: "Nu există activitate de audit aferentă dosarului.",
    },
    hu: {
      title: "Ügy aktivitás",
      empty: "Nincs kapcsolódó audit tevékenység.",
    },
  }[locale];

  useEffect(() => {
    let mounted = true;
    const loadLogs = async () => {
      setLoading(true);
      try {
        const [byEntity, byCaseNumber] = await Promise.all([
          auditLogsApi.getAll({ entityId: caseId, pageSize: 100 }),
          caseNumber ? auditLogsApi.getAll({ search: caseNumber, pageSize: 100 }) : Promise.resolve({ data: { items: [], total: 0, page: 0, pageSize: 100 } }),
        ]);

        if (!mounted) return;

        const merged = new Map<string, import("@/services/api/types").AuditLogDto>();
        for (const item of byEntity.data.items) merged.set(item.id, item);
        for (const item of byCaseNumber.data.items) merged.set(item.id, item);

        setLogs(Array.from(merged.values()).sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()));
      } catch (error) {
        console.error(error);
      } finally {
        if (mounted) setLoading(false);
      }
    };

    void loadLogs();
    return () => { mounted = false; };
  }, [caseId, caseNumber]);

  if (loading) return <div className="p-6 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" /></div>;

  return (
    <div className="rounded-xl border border-border bg-card divide-y divide-border">
      <div className="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{texts.title}</div>
      {logs.length === 0 ? (
        <p className="px-4 py-6 text-center text-sm text-muted-foreground">{texts.empty}</p>
      ) : (
        logs.map(log => (
          <div key={log.id} className="px-4 py-2.5">
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="text-sm font-medium text-foreground truncate">{log.action}</p>
                <p className="text-[11px] text-muted-foreground truncate">{log.description || log.entityType || log.category}</p>
              </div>
              <Badge variant="outline" className="text-[10px] shrink-0">{log.severity}</Badge>
            </div>
            <div className="mt-1 text-[10px] text-muted-foreground">
              {format(new Date(log.timestamp), "dd MMM yyyy HH:mm")} {log.userEmail ? `· ${log.userEmail}` : ""}
            </div>
          </div>
        ))
      )}
    </div>
  );
}
/* ── Templates Tab Component ─────────────────────────── */
function TemplatesTab({ caseId }: { caseId: string }) {
  const [templates, setTemplates] = useState<import("@/services/api/documentTemplatesApi").DocumentTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [downloading, setDownloading] = useState<string | null>(null);

  useEffect(() => {
    documentTemplatesApi.getAll()
      .then(r => setTemplates(
        r.data.filter(t => t.isSystem && String(t.templateType) !== "CourtOpeningDecision")
      ))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleDownloadPdf = async (tpl: import("@/services/api/documentTemplatesApi").DocumentTemplateDto) => {
    setDownloading(tpl.id);
    try {
      const { blob, fileName } = await documentTemplatesApi.renderPdfBlob(tpl.id, { caseId });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) { console.error(err); }
    finally { setDownloading(null); }
  };

  const friendlyType = (type: string) => {
    const map: Record<string, string> = {
      CreditorNotificationBpi: "Notificare Creditori (BPI)",
      CreditorNotificationHtml: "Notificare Deschidere Procedură",
      ReportArt97: "Raport Art. 97 (40 zile)",
      PreliminaryClaimsTable: "Tabel Preliminar de Creanțe",
      CreditorsMeetingMinutes: "Proces-Verbal AGC",
      DefinitiveClaimsTable: "Tabel Definitiv de Creanțe",
      FinalReportArt167: "Raport Final Art. 167",
    };
    return map[type] ?? type.replace(/([A-Z])/g, " $1").trim();
  };

  if (loading) return (
    <div className="p-6 text-center">
      <Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" />
    </div>
  );

  return (
    <div className="space-y-3">
      <div className="rounded-xl border border-border bg-card p-4">
        <h3 className="text-sm font-semibold text-foreground">Documente Insolvență</h3>
        <p className="text-xs text-muted-foreground mt-0.5">
          Generează documentele dosarului din șabloanele HTML configurate. Fișierul PDF este descărcat direct.
        </p>
      </div>

      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {templates.length === 0 && (
          <p className="px-4 py-6 text-center text-sm text-muted-foreground">
            Niciun șablon activ. Accesează <strong>Setări → Șabloane Documente</strong> pentru configurare.
          </p>
        )}
        {templates.map(tpl => {
          const isBusy = downloading === tpl.id;
          return (
            <div key={tpl.id} className="flex items-center gap-3 px-4 py-3">
              <FileText className={`h-4 w-4 shrink-0 ${tpl.hasContent ? "text-primary" : "text-muted-foreground/40"}`} />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-foreground">{tpl.name}</p>
                  {tpl.category && (
                    <span className="inline-flex rounded px-1.5 py-0.5 text-[10px] font-medium bg-muted text-muted-foreground">
                      {tpl.category}
                    </span>
                  )}
                  {!tpl.hasContent && (
                    <span className="inline-flex rounded px-1.5 py-0.5 text-[10px] font-medium bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300">
                      Fără conținut
                    </span>
                  )}
                </div>
                <p className="text-[10px] text-muted-foreground">{friendlyType(tpl.templateType)}</p>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="text-xs gap-1.5 h-7 shrink-0"
                onClick={() => handleDownloadPdf(tpl)}
                disabled={!tpl.hasContent || isBusy}
              >
                {isBusy ? <Loader2 className="h-3 w-3 animate-spin" /> : <Download className="h-3 w-3" />}
                {isBusy ? "Se generează..." : "Descarcă PDF"}
              </Button>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ── Add Party Modal Component ─────────────────────────── */
const PARTY_ROLES: { value: string; translationKey: keyof import("@/i18n/types").Translations["partyRoles"] }[] = [
  { value: "Debtor", translationKey: "debtor" },
  { value: "InsolvencyPractitioner", translationKey: "insolvencyPractitioner" },
  { value: "SecuredCreditor", translationKey: "securedCreditor" },
  { value: "UnsecuredCreditor", translationKey: "unsecuredCreditor" },
  { value: "BudgetaryCreditor", translationKey: "budgetaryCreditor" },
  { value: "EmployeeCreditor", translationKey: "employeeCreditor" },
  { value: "JudgeSyndic", translationKey: "judgeSyndic" },
  { value: "CourtExpert", translationKey: "courtExpert" },
  { value: "CreditorsCommittee", translationKey: "creditorsCommittee" },
  { value: "SpecialAdministrator", translationKey: "specialAdministrator" },
  { value: "Guarantor", translationKey: "guarantor" },
  { value: "ThirdParty", translationKey: "thirdParty" },
];

interface AddPartyModalProps {
  caseId: string;
  locale: "en" | "ro" | "hu";
  onAdded: () => void;
  onClose: () => void;
}

function AddPartyModal({ caseId, locale, onAdded, onClose }: AddPartyModalProps) {
  const [query, setQuery] = useState("");
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [loadingCompanies, setLoadingCompanies] = useState(false);
  const [onrcResults, setOnrcResults] = useState<ONRCFirmResult[]>([]);
  const [onrcLoading, setOnrcLoading] = useState(false);
  const [onrcSearched, setOnrcSearched] = useState(false);
  const [selected, setSelected] = useState<CompanyDto | null>(null);
  const { t } = useTranslation();
  const [role, setRole] = useState("UnsecuredCreditor");
  const [roleDescription, setRoleDescription] = useState("");
  const [claimAmount, setClaimAmount] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isCreditorRole = ["SecuredCreditor", "UnsecuredCreditor", "BudgetaryCreditor", "EmployeeCreditor"].includes(role);

  const uiText = {
    en: {
      title: "Add Party to Case",
      company: "Company",
      role: "Role",
      roleDescription: "Role Description",
      optional: "optional",
      claim: "Claim (RON)",
      searchPlaceholder: "Search by name or CUI...",
      noLocal: "No local company found. Click ONRC to search the national registry.",
      noAny: "No company found locally or in ONRC.",
      localCompanies: "Local companies",
      onrcRegistry: "ONRC Registry",
      noCui: "No CUI",
      save: "Add Party",
      saving: "Saving...",
      cancel: "Cancel",
      selectCompanyError: "Select a company.",
      selectRoleError: "Select a role.",
      addPartyError: "Error adding party.",
      createFromOnrcError: "Error creating company from ONRC.",
      rolePlaceholder: "e.g. Secured rank I creditor",
      onrcTitle: "Search ONRC national registry",
      onrcSkipped: "Exact match found locally — ONRC search skipped",
    },
    ro: {
      title: "Adaugă Parte la Dosar",
      company: "Companie",
      role: "Rol",
      roleDescription: "Descriere Rol",
      optional: "opțional",
      claim: "Creanță (RON)",
      searchPlaceholder: "Caută după nume sau CUI...",
      noLocal: "Nicio companie locală găsită. Apasă ONRC pentru a căuta în registrul național.",
      noAny: "Nicio companie găsită nici local, nici în ONRC.",
      localCompanies: "Companii locale",
      onrcRegistry: "Registrul ONRC",
      noCui: "Fără CUI",
      save: "Adaugă Parte",
      saving: "Se salvează...",
      cancel: "Anulează",
      selectCompanyError: "Selectează o companie.",
      selectRoleError: "Selectează un rol.",
      addPartyError: "Eroare la adăugarea părții.",
      createFromOnrcError: "Eroare la crearea companiei din ONRC.",
      rolePlaceholder: "ex: Creditor ipotecar rang I",
      onrcTitle: "Caută în registrul național ONRC",
      onrcSkipped: "Potrivire exactă găsită local — căutarea ONRC a fost omisă",
    },
    hu: {
      title: "Fél hozzáadása az ügyhöz",
      company: "Cég",
      role: "Szerep",
      roleDescription: "Szerep leírás",
      optional: "opcionális",
      claim: "Követelés (RON)",
      searchPlaceholder: "Keresés név vagy CUI alapján...",
      noLocal: "Nem található helyi cég. Kattints az ONRC-re az országos kereséshez.",
      noAny: "Nem található cég sem helyben, sem az ONRC-ben.",
      localCompanies: "Helyi cégek",
      onrcRegistry: "ONRC nyilvántartás",
      noCui: "Nincs CUI",
      save: "Fél hozzáadása",
      saving: "Mentés...",
      cancel: "Mégse",
      selectCompanyError: "Válassz céget.",
      selectRoleError: "Válassz szerepet.",
      addPartyError: "Hiba a fél hozzáadásakor.",
      createFromOnrcError: "Hiba a cég ONRC-ből történő létrehozásakor.",
      rolePlaceholder: "pl. 1. rangú biztosított hitelező",
      onrcTitle: "Keresés az ONRC országos nyilvántartásban",
      onrcSkipped: "Pontos egyezés helyben megtalálva — ONRC keresés kihagyva",
    },
  }[locale];

  // Check if we have an exact match in local results (by CUI or exact name)
  const hasExactMatch = companies.some(c => {
    const q = query.trim().toLowerCase();
    return (c.cuiRo && c.cuiRo.toLowerCase() === q) ||
           c.name.toLowerCase() === q;
  });

  // Search local DB as user types (debounced)
  useEffect(() => {
    if (!query.trim() || query.trim().length < 2) {
      setCompanies([]);
      setOnrcResults([]);
      setOnrcSearched(false);
      return;
    }
    const timer = setTimeout(() => {
      setLoadingCompanies(true);
      companiesApi.search(query.trim(), 10)
        .then(r => setCompanies(r.data))
        .catch(console.error)
        .finally(() => setLoadingCompanies(false));
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  // Search ONRC on button click
  const searchOnrc = async () => {
    if (query.trim().length < 2 || hasExactMatch) return;
    setOnrcLoading(true);
    try {
      const r = await onrcApi.search(query.trim(), "Romania", 10);
      setOnrcResults(r.data);
      setOnrcSearched(true);
    } catch (e) { console.error(e); }
    finally { setOnrcLoading(false); }
  };

  const handleSubmit = async () => {
    if (!selected) { setError(uiText.selectCompanyError); return; }
    if (!role) { setError(uiText.selectRoleError); return; }
    setSaving(true);
    setError(null);
    try {
      await casesApi.addParty(caseId, {
        companyId: selected.id,
        role,
        roleDescription: roleDescription || undefined,
        claimAmountRon: claimAmount ? parseFloat(claimAmount) : undefined,
      } as Parameters<typeof casesApi.addParty>[1]);
      onAdded();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? uiText.addPartyError);
    } finally { setSaving(false); }
  };

  // Select an ONRC result — create company locally first, then select it
  const selectOnrc = async (firm: ONRCFirmResult) => {
    setSaving(true);
    setError(null);
    try {
      const r = await companiesApi.create({
        name: firm.name,
        cuiRo: firm.cui,
        tradeRegisterNo: firm.tradeRegisterNo ?? undefined,
        address: firm.address ?? undefined,
        locality: firm.locality ?? undefined,
        county: firm.county ?? undefined,
        postalCode: firm.postalCode ?? undefined,
        caen: firm.caen ?? undefined,
        phone: firm.phone ?? undefined,
        incorporationYear: firm.incorporationYear ?? undefined,
      } as Partial<CompanyDto>);
      setSelected(r.data);
      setQuery("");
      setCompanies([]);
      setOnrcResults([]);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? uiText.createFromOnrcError);
    } finally { setSaving(false); }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-lg rounded-xl border border-border bg-card shadow-xl p-5 mx-4"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-semibold text-foreground flex items-center gap-2">
            <Users className="h-4 w-4 text-primary" /> {uiText.title}
          </h2>
          <button onClick={onClose} className="rounded-md p-1 hover:bg-muted transition-colors">
            <X className="h-4 w-4 text-muted-foreground" />
          </button>
        </div>

        <div className="space-y-3">
          {/* Company search */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{uiText.company}</label>
            {selected ? (
              <div className="mt-1 flex items-center justify-between rounded-md border border-border bg-muted/40 px-3 py-2">
                <div>
                  <p className="text-sm font-medium text-foreground">{selected.name}</p>
                  {selected.cuiRo && <p className="text-[10px] text-muted-foreground">CUI: {selected.cuiRo}</p>}
                </div>
                <button onClick={() => { setSelected(null); setQuery(""); }} className="rounded p-0.5 hover:bg-muted">
                  <X className="h-3.5 w-3.5 text-muted-foreground" />
                </button>
              </div>
            ) : (
              <div className="mt-1 space-y-2">
                {/* Search input + ONRC button */}
                <div className="flex gap-2">
                  <div className="relative flex-1">
                    <div className="flex items-center rounded-md border border-border bg-background px-3 gap-2">
                      <Search className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                      <input
                        value={query}
                        onChange={e => { setQuery(e.target.value); setOnrcResults([]); setOnrcSearched(false); }}
                        onKeyDown={e => e.key === "Enter" && (e.preventDefault(), searchOnrc())}
                        placeholder={uiText.searchPlaceholder}
                        className="flex-1 py-1.5 text-sm bg-transparent focus:outline-none"
                        autoFocus
                      />
                      {loadingCompanies && <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground shrink-0" />}
                    </div>
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={searchOnrc}
                    disabled={onrcLoading || query.trim().length < 2 || hasExactMatch}
                    className="shrink-0 gap-1 text-xs h-auto"
                    title={hasExactMatch ? uiText.onrcSkipped : uiText.onrcTitle}
                  >
                    {onrcLoading ? <Loader2 className="h-3 w-3 animate-spin" /> : <Search className="h-3 w-3" />}
                    ONRC
                  </Button>
                </div>

                {/* Results dropdown */}
                {(companies.length > 0 || onrcResults.length > 0) && (
                  <div className="rounded-md border border-border bg-card shadow-lg max-h-56 overflow-y-auto">
                    {/* Local results */}
                    {companies.length > 0 && (
                      <>
                        <p className="px-3 py-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0 z-10">
                          {uiText.localCompanies}
                        </p>
                        {companies.map(c => (
                          <button
                            key={c.id}
                            className="w-full text-left px-3 py-2 hover:bg-muted transition-colors flex items-start gap-2"
                            onClick={() => { setSelected(c); setQuery(""); setCompanies([]); setOnrcResults([]); }}
                          >
                            <Building2 className="h-3.5 w-3.5 mt-0.5 shrink-0 text-emerald-500" />
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium text-foreground">{c.name}</p>
                              <p className="text-[10px] text-muted-foreground">
                                {c.cuiRo ? `CUI: ${c.cuiRo}` : uiText.noCui}
                                {c.caseNumbers && c.caseNumbers.length > 0 ? ` · ${c.caseNumbers.length} dosar(e)` : ""}
                              </p>
                            </div>
                            <span className="ml-auto shrink-0 self-center text-[10px] px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400">
                              local
                            </span>
                          </button>
                        ))}
                      </>
                    )}
                    {/* ONRC results */}
                    {onrcResults.length > 0 && (
                      <>
                        <p className="px-3 py-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0 z-10">
                          {uiText.onrcRegistry}
                        </p>
                        {onrcResults.map(r => (
                          <button
                            key={r.id}
                            className="w-full text-left px-3 py-2 hover:bg-muted transition-colors flex items-start gap-2"
                            onClick={() => selectOnrc(r)}
                          >
                            <Building2 className="h-3.5 w-3.5 mt-0.5 shrink-0 text-primary" />
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium text-foreground">{r.name}</p>
                              <p className="text-[10px] text-muted-foreground">
                                CUI: {r.cui}
                                {r.locality ? ` · ${r.locality}` : ""}
                                {r.county ? `, ${r.county}` : ""}
                              </p>
                            </div>
                            <span className="ml-auto shrink-0 self-center text-[10px] px-1.5 py-0.5 rounded bg-primary/10 text-primary">
                              ONRC
                            </span>
                          </button>
                        ))}
                      </>
                    )}
                  </div>
                )}

                {/* No results message */}
                {query.trim().length >= 2 && !loadingCompanies && companies.length === 0 && !onrcSearched && (
                  <p className="text-xs text-muted-foreground px-1">
                    {uiText.noLocal}
                  </p>
                )}
                {query.trim().length >= 2 && onrcSearched && companies.length === 0 && onrcResults.length === 0 && (
                  <p className="text-xs text-muted-foreground px-1">
                    {uiText.noAny}
                  </p>
                )}
              </div>
            )}
          </div>

          {/* Role */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{uiText.role}</label>
            <select
              value={role}
              onChange={e => setRole(e.target.value)}
              className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
            >
              {PARTY_ROLES.map(r => (
                <option key={r.value} value={r.value}>{t.partyRoles[r.translationKey]}</option>
              ))}
            </select>
          </div>

          {/* Role description */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
              {uiText.roleDescription} <span className="font-normal text-muted-foreground/60">({uiText.optional})</span>
            </label>
            <input
              value={roleDescription}
              onChange={e => setRoleDescription(e.target.value)}
              placeholder={uiText.rolePlaceholder}
              className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
            />
          </div>

          {/* Claim amount — only for creditor roles */}
          {isCreditorRole && (
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                {uiText.claim} <span className="font-normal text-muted-foreground/60">({uiText.optional})</span>
              </label>
              <input
                type="number"
                min="0"
                step="0.01"
                value={claimAmount}
                onChange={e => setClaimAmount(e.target.value)}
                placeholder="0.00"
                className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
              />
            </div>
          )}

          {error && <p className="text-xs text-destructive">{error}</p>}

          {/* Actions */}
          <div className="flex gap-2 pt-1">
            <Button
              className="flex-1 gap-1.5 text-xs"
              onClick={handleSubmit}
              disabled={saving || !selected}
            >
              {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />}
              {saving ? uiText.saving : uiText.save}
            </Button>
            <Button variant="outline" className="text-xs" onClick={onClose} disabled={saving}>
              {uiText.cancel}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}