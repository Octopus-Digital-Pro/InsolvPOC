import { useState, useEffect, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { casesApi } from "@/services/api";
import { workflowApi } from "@/services/api/workflow";
import type { GeneratedDocResult, TemplateInfo } from "@/services/api/workflow";
import { caseEmailsApi } from "@/services/api/caseWorkspace";
import { tasksApi } from "@/services/api/tasks";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CaseDto, CasePartyDto, CasePhaseDto, DocumentDto, TaskDto } from "@/services/api/types";
import type { CaseEmailDto } from "@/services/api/caseWorkspace";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import StageTimeline from "@/components/StageTimeline";
import CreditorMeetingModal from "@/components/CreditorMeetingModal";
import DocumentSigningPanel from "@/components/DocumentSigningPanel";
import CaseTasksTab from "@/components/CaseTasksTab";
import CaseEmailsTab from "@/components/CaseEmailsTab";
import CaseEventFeed from "@/components/CaseEventFeed";
import { downloadAuthFile } from "@/utils/downloadAuthFile";
import {
  Loader2, FileText, Upload, Users, GitBranch, ChevronRight,
  Check, Clock, Ban, SkipForward, Brain, CalendarDays, RefreshCw,
  ListChecks, Mail, Download, FileOutput, ChevronDown, Pencil,
  X, Save, Wand2, History, AlertTriangle,
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
  const { t } = useTranslation();
  const [caseData, setCaseData] = useState<CaseDto | null>(null);
  const [parties, setParties] = useState<CasePartyDto[]>([]);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [advancing, setAdvancing] = useState(false);
  const [advanceError, setAdvanceError] = useState<string | null>(null);
  const [meetingOpen, setMeetingOpen] = useState(false);
  const [summary, setSummary] = useState<Record<string, unknown> | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"overview" | "tasks" | "docs" | "parties" | "emails" | "calendar" | "templates" | "activity">("overview");
  const [caseTasks, setCaseTasks] = useState<TaskDto[]>([]);
  const [caseEmails, setCaseEmails] = useState<CaseEmailDto[]>([]);
  const [docUploading, setDocUploading] = useState(false);
  const docUploadRef = useRef<HTMLInputElement>(null);

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

  const handleInitPhases = async () => {
    if (!id) return;
    try {
      await casesApi.initializePhases(id);
      load();
    } catch (err) { console.error(err); }
  };

  const handleAdvance = async () => {
    if (!id) return;
    setAdvancing(true);
    setAdvanceError(null);
    try {
      await casesApi.advancePhase(id);
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setAdvanceError(msg ?? "Failed to advance phase.");
    } finally { setAdvancing(false); }
  };;

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

  const stageLabel = (s: string): string =>
    (t.stages as Record<string, string>)[s] ?? s.replace(/([A-Z])/g, " $1").trim();

  const phaseLabel = (phaseType: string): string => {
    const key = phaseType.charAt(0).toLowerCase() + phaseType.slice(1);
    return (t.phases as Record<string, string>)[key] ?? phaseType.replace(/([A-Z])/g, " $1").trim();
  };

  const phaseStatusLabel = (status: string): string => {
    const key = status.charAt(0).toLowerCase() + status.slice(1);
    return (t.phaseStatus as Record<string, string>)[key] ?? status;
  };

  const partyRoleLabel = (role: string): string => {
    const key = role.charAt(0).toLowerCase() + role.slice(1);
    return (t.partyRoles as Record<string, string>)[key] ?? role.replace(/([A-Z])/g, " $1").trim();
  };

  const phaseStatusIcon = (status: string) => {
    switch (status) {
      case "Completed": return <Check className="h-3.5 w-3.5 text-green-500" />;
      case "InProgress": return <Clock className="h-3.5 w-3.5 text-blue-500 animate-pulse" />;
      case "Blocked": return <Ban className="h-3.5 w-3.5 text-red-500" />;
      case "Skipped": return <SkipForward className="h-3.5 w-3.5 text-muted-foreground" />;
      default: return <div className="h-3.5 w-3.5 rounded-full border-2 border-muted-foreground/30" />;
    }
  };

  const phaseStatusVariant = (status: string): "success" | "default" | "destructive" | "secondary" | "warning" => {
    switch (status) {
      case "Completed": return "success";
      case "InProgress": return "default";
      case "Blocked": return "destructive";
      case "Skipped": return "secondary";
      default: return "secondary";
    }
  };

  const phases = caseData.phases ?? [];
  const hasPhases = phases.length > 0;
  const currentPhase = phases.find(p => p.status === "InProgress");

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
          <Badge variant="secondary">{stageLabel(caseData.stage)}</Badge>
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

      {/* Workspace layout: sidebar + main */}
      <div className="flex gap-4">
        {/* Left sidebar: Stage Timeline + Actions */}
        <div className="w-56 shrink-0 space-y-3">
          <div className="rounded-xl border border-border bg-card p-3">
            <StageTimeline caseId={id!} onAdvanced={load} />
          </div>
          <Button
            variant="outline"
            size="sm"
            className="w-full text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
            onClick={() => setMeetingOpen(true)}
          >
            <Users className="h-3.5 w-3.5" />
            Call Creditor Meeting
          </Button>
        </div>

        {/* Main panel with tabs */}
        <div className="flex-1 min-w-0 space-y-3">
          {/* Tab bar */}
          <div className="flex gap-1 rounded-lg border border-border bg-card p-1 overflow-x-auto">
            {([
              { id: "overview" as const, label: "Overview", icon: Brain },
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

              {/* Workflow Phases */}
              <div>
                <div className="mb-2 flex items-center justify-between">
                  <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    <GitBranch className="h-3.5 w-3.5" /> {t.phases.title} {hasPhases && `(${phases.filter(p => p.status === "Completed").length}/${phases.length})`}
                  </h2>
                  {hasPhases && currentPhase && (() => {
                    const openCount = caseTasks.filter(t => t.status === "open" || t.status === "blocked").length;
                    const isBlocked = openCount > 0;
                    return (
                      <div className="flex flex-col items-end gap-1">
                        <Button
                          variant="outline"
                          size="sm"
                          className={`text-xs gap-1 ${isBlocked ? "border-amber-300 text-amber-700 hover:bg-amber-50 dark:border-amber-700 dark:text-amber-400" : "border-primary/30 text-primary hover:bg-primary/5"}`}
                          onClick={handleAdvance}
                          disabled={advancing || isBlocked}
                          title={isBlocked ? `${openCount} open task${openCount !== 1 ? "s" : ""} must be completed first` : "Advance to next phase"}
                        >
                          {advancing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : isBlocked ? <AlertTriangle className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                          {isBlocked ? `${openCount} open task${openCount !== 1 ? "s" : ""}` : "Advance"}
                        </Button>
                        {advanceError && (
                          <p className="text-[10px] text-destructive max-w-xs text-right">{advanceError}</p>
                        )}
                      </div>
                    );
                  })()}
                  {!hasPhases && (
                    <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5" onClick={handleInitPhases}>
                      Initialize Workflow
                    </Button>
                  )}
                </div>
                {hasPhases ? (
                  <div className="rounded-xl border border-border bg-card divide-y divide-border">
                    {phases.map((phase) => (
                      <PhaseRow
                        key={phase.id}
                        caseId={id!}
                        phase={phase}
                        tasks={caseTasks}
                        phaseLabel={phaseLabel}
                        phaseStatusLabel={phaseStatusLabel}
                        phaseStatusIcon={phaseStatusIcon}
                        phaseStatusVariant={phaseStatusVariant}
                        onUpdated={load}
                      />
                    ))}
                  </div>
                ) : (
                  <div className="rounded-xl border border-dashed border-border bg-card/50 p-6 text-center">
                    <p className="text-sm text-muted-foreground">No workflow phases initialized yet.</p>
                  </div>
                )}
              </div>
            </div>
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
            <CaseEventFeed caseId={id!} />
          )}
        </div>
      </div>

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

/* ── Phase Row Component ─────────────────────────── */
interface PhaseRowProps {
  caseId: string;
  phase: CasePhaseDto;
  tasks: TaskDto[];
  phaseLabel: (type: string) => string;
  phaseStatusLabel: (status: string) => string;
  phaseStatusIcon: (status: string) => JSX.Element;
  phaseStatusVariant: (status: string) => "success" | "default" | "destructive" | "secondary" | "warning";
  onUpdated: () => void;
}

function PhaseRow({ caseId, phase, tasks, phaseLabel, phaseStatusLabel, phaseStatusIcon, phaseStatusVariant, onUpdated }: PhaseRowProps) {
  const [expanded, setExpanded] = useState(phase.status === "InProgress");
  const [editing, setEditing] = useState(false);
  const [notes, setNotes] = useState(phase.notes ?? "");
  const [courtRef, setCourtRef] = useState(phase.courtDecisionRef ?? "");
  const [dueDate, setDueDate] = useState(phase.dueDate ? format(new Date(phase.dueDate), "yyyy-MM-dd") : "");
  const [saving, setSaving] = useState(false);
  const [genTasksLoading, setGenTasksLoading] = useState(false);
  const [requirements, setRequirements] = useState<{ requiredTasks: string[]; requiredDocTypes: string[]; goal: string } | null>(null);

  const phaseTasks = tasks.filter(t =>
    (t as unknown as Record<string, unknown>).caseId === caseId
  );

  useEffect(() => {
    if (expanded && !requirements) {
      casesApi.getPhaseRequirements(caseId, phase.id)
        .then(r => setRequirements(r.data))
        .catch(console.error);
    }
  }, [expanded, caseId, phase.id, requirements]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await casesApi.updatePhase(caseId, phase.id, {
        notes: notes || undefined,
        courtDecisionRef: courtRef || undefined,
        dueDate: dueDate ? new Date(dueDate).toISOString() : undefined,
      } as Parameters<typeof casesApi.updatePhase>[2]);
      setEditing(false);
      onUpdated();
    } catch (err) { console.error(err); }
    finally { setSaving(false); }
  };

  const handleGenerateTasks = async () => {
    setGenTasksLoading(true);
    try {
      await casesApi.generatePhaseTasks(caseId, phase.id);
      onUpdated();
    } catch (err) { console.error(err); }
    finally { setGenTasksLoading(false); }
  };

  const doneTasks = phaseTasks.filter(t => t.status === "done").length;
  const totalTasks = phaseTasks.length;

  return (
    <div className={phase.status === "InProgress" ? "bg-primary/5" : ""}>
      {/* Header row */}
      <div
        className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-muted/30 transition-colors"
        onClick={() => setExpanded(e => !e)}
      >
        <div className="flex items-center justify-center w-6 shrink-0">
          {phaseStatusIcon(phase.status)}
        </div>
        <div className="min-w-0 flex-1">
          <p className={`text-sm font-medium ${phase.status === "Completed" ? "text-muted-foreground line-through" : phase.status === "InProgress" ? "text-primary" : "text-foreground"}`}>
            {phaseLabel(phase.phaseType)}
          </p>
          {phase.courtDecisionRef && !expanded && (
            <p className="text-[10px] text-muted-foreground truncate">{phase.courtDecisionRef}</p>
          )}
        </div>
        {totalTasks > 0 && (
          <span className="text-[10px] text-muted-foreground shrink-0">{doneTasks}/{totalTasks}</span>
        )}
        <Badge variant={phaseStatusVariant(phase.status)} className="text-[10px] shrink-0">
          {phaseStatusLabel(phase.status)}
        </Badge>
        {phase.dueDate && !expanded && (
          <span className="text-[10px] text-muted-foreground shrink-0">
            {format(new Date(phase.dueDate), "dd MMM")}
          </span>
        )}
        <ChevronDown className={`h-3.5 w-3.5 text-muted-foreground shrink-0 transition-transform ${expanded ? "rotate-180" : ""}`} />
      </div>

      {/* Expanded panel */}
      {expanded && (
        <div className="px-4 pb-4 pt-1 border-t border-border/60 space-y-3">
          {/* Goal */}
          {requirements?.goal && (
            <p className="text-xs text-muted-foreground italic">{requirements.goal}</p>
          )}

          {/* Fields edit / display */}
          {editing ? (
            <div className="space-y-2.5">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                <div>
                  <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Court Decision Ref</label>
                  <input
                    value={courtRef}
                    onChange={e => setCourtRef(e.target.value)}
                    placeholder="e.g. Dosar 1234/2024"
                    className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
                <div>
                  <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Due Date</label>
                  <input
                    type="date"
                    value={dueDate}
                    onChange={e => setDueDate(e.target.value)}
                    className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>
              <div>
                <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Notes</label>
                <textarea
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                  rows={2}
                  placeholder="Phase notes, observations..."
                  className="mt-1 w-full rounded-md border border-border bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary resize-none"
                />
              </div>
              <div className="flex gap-2">
                <Button size="sm" className="text-xs h-7 gap-1" onClick={handleSave} disabled={saving}>
                  {saving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Save className="h-3 w-3" />}
                  Save
                </Button>
                <Button size="sm" variant="ghost" className="text-xs h-7 gap-1" onClick={() => setEditing(false)}>
                  <X className="h-3 w-3" /> Cancel
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap gap-4">
              {phase.courtDecisionRef && (
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Court Decision</p>
                  <p className="text-sm">{phase.courtDecisionRef}</p>
                </div>
              )}
              {phase.dueDate && (
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Due Date</p>
                  <p className="text-sm">{format(new Date(phase.dueDate), "dd MMM yyyy")}</p>
                </div>
              )}
              {phase.startedOn && (
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Started</p>
                  <p className="text-sm">{format(new Date(phase.startedOn), "dd MMM yyyy")}</p>
                </div>
              )}
              {phase.completedOn && (
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Completed</p>
                  <p className="text-sm">{format(new Date(phase.completedOn), "dd MMM yyyy")}</p>
                </div>
              )}
              {phase.notes && (
                <div className="w-full">
                  <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Notes</p>
                  <p className="text-sm text-muted-foreground">{phase.notes}</p>
                </div>
              )}
              {!phase.courtDecisionRef && !phase.dueDate && !phase.notes && (
                <p className="text-xs text-muted-foreground/60 italic">No details added yet.</p>
              )}
              <Button variant="ghost" size="sm" className="h-6 px-1.5 text-[10px] gap-1 ml-auto"
                onClick={() => setEditing(true)}>
                <Pencil className="h-3 w-3" /> Edit
              </Button>
            </div>
          )}

          {/* Required tasks checklist */}
          {requirements && requirements.requiredTasks.length > 0 && (
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  Required Tasks ({doneTasks}/{requirements.requiredTasks.length})
                </p>
                <Button variant="ghost" size="sm" className="h-6 px-1.5 text-[10px] gap-1"
                  onClick={handleGenerateTasks} disabled={genTasksLoading}>
                  {genTasksLoading ? <Loader2 className="h-3 w-3 animate-spin" /> : <Wand2 className="h-3 w-3" />}
                  Generate Tasks
                </Button>
              </div>
              <div className="space-y-1">
                {requirements.requiredTasks.map((taskTitle, i) => {
                  const matchingTask = phaseTasks.find(t =>
                    t.title.toLowerCase().includes(taskTitle.toLowerCase()) ||
                    taskTitle.toLowerCase().includes(t.title.toLowerCase().split(" – ")[0])
                  );
                  const isDone = matchingTask?.status === "done";
                  return (
                    <div key={i} className="flex items-start gap-2">
                      <div className={`mt-0.5 h-3.5 w-3.5 rounded-full border-2 shrink-0 flex items-center justify-center ${isDone ? "border-green-500 bg-green-500" : "border-border"}`}>
                        {isDone && <Check className="h-2 w-2 text-white" />}
                      </div>
                      <span className={`text-xs ${isDone ? "line-through text-muted-foreground" : "text-foreground"}`}>
                        {taskTitle}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Required document types */}
          {requirements && requirements.requiredDocTypes.length > 0 && (
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground mb-1">Required Documents</p>
              <div className="flex flex-wrap gap-1.5">
                {requirements.requiredDocTypes.map(dt => (
                  <Badge key={dt} variant="outline" className="text-[10px]">{dt}</Badge>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
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
/* ── Templates Tab Component ─────────────────────────── */
function TemplatesTab({ caseId }: { caseId: string }) {
  const [templates, setTemplates] = useState<TemplateInfo[]>([]);
  const [generated, setGenerated] = useState<GeneratedDocResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState<string | null>(null);
  const [generatingAll, setGeneratingAll] = useState(false);

  useEffect(() => {
    workflowApi.mailMerge.getTemplates()
      .then(r => setTemplates(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleGenerate = async (templateType: string) => {
    setGenerating(templateType);
    try {
      const r = await workflowApi.mailMerge.generate(caseId, templateType);
      setGenerated(prev => [r.data, ...prev.filter(g => g.templateType !== templateType)]);
    } catch (err) { console.error(err); }
    finally { setGenerating(null); }
  };

  const handleGenerateAll = async () => {
    setGeneratingAll(true);
    try {
      const r = await workflowApi.mailMerge.generateAll(caseId);
      setGenerated(r.data);
    } catch (err) { console.error(err); }
    finally { setGeneratingAll(false); }
  };

  const friendlyName = (type: string) => {
    const labels: Record<string, string> = {
      CourtOpeningDecision: "Court Opening Decision",
      CreditorNotificationBpi: "Notificare Creditori (BPI)",
      CreditorNotificationHtml: "Notificare Deschidere Procedură (PDF)",
      ReportArt97: "Raport Art. 97 (40 zile)",
      PreliminaryClaimsTable: "Tabel Preliminar de Creanțe",
      CreditorsMeetingMinutes: "Proces-Verbal AGC",
      DefinitiveClaimsTable: "Tabel Definitiv de Creanțe",
      FinalReportArt167: "Raport Final Art. 167",
    };
    return labels[type] ?? type.replace(/([A-Z])/g, " $1").trim();
  };

  const getToken = () => localStorage.getItem("authToken");
  const getTenantId = () => localStorage.getItem("selectedTenantId");

  const downloadDoc = (key: string, fileName: string) => {
    const token = getToken();
    const tenantId = getTenantId();
    fetch(`/api/mailmerge/download?key=${encodeURIComponent(key)}`, {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
      },
    }).then(r => r.blob()).then(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    });
  };

  if (loading) return <div className="p-6 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" /></div>;

  return (
    <div className="space-y-3">
      {/* Header with Generate All */}
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-sm font-semibold text-foreground">Mail Merge Templates (Templates-Ro)</h3>
            <p className="text-xs text-muted-foreground mt-0.5">Generate case documents from predefined Romanian insolvency templates</p>
          </div>
          <Button size="sm" className="gap-1.5 text-xs" onClick={handleGenerateAll} disabled={generatingAll}>
            {generatingAll ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileOutput className="h-3.5 w-3.5" />}
            Generate All
          </Button>
        </div>
      </div>

      {/* Template list */}
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {templates.length === 0 && (
          <p className="px-4 py-6 text-center text-sm text-muted-foreground">No templates available. Add .doc files to Templates-Ro folder.</p>
        )}
        {templates.map(tpl => {
          const result = generated.find(g => g.templateType === tpl.templateType);
          const isGenerating = generating === tpl.templateType;
          const hasTemplate = tpl.effectiveSource !== "missing";
          const displayFile = tpl.tenantOverrideFileName ?? tpl.globalOverrideFileName ?? tpl.defaultFileName;
          return (
            <div key={tpl.templateType} className="flex items-center gap-3 px-4 py-3">
              <FileText className={`h-4 w-4 shrink-0 ${hasTemplate ? "text-primary" : "text-muted-foreground/40"}`} />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-foreground">{friendlyName(tpl.templateType)}</p>
                  {tpl.templateType === "CreditorNotificationHtml" && (
                    <span className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-medium bg-violet-100 text-violet-700 dark:bg-violet-900/30 dark:text-violet-300">
                      HTML→PDF
                    </span>
                  )}
                </div>
                <p className="text-[10px] text-muted-foreground">{displayFile}{!hasTemplate && " — template not available"}</p>
              </div>
              {result && (
                <Button variant="ghost" size="sm" className="text-xs gap-1 h-7 text-primary"
                  onClick={() => downloadDoc(result.storageKey, result.fileName)}>
                  <Download className="h-3 w-3" />
                  {result.fileName}
                </Button>
              )}
              <Button variant="outline" size="sm" className="text-xs gap-1 h-7 shrink-0"
                onClick={() => handleGenerate(tpl.templateType)}
                disabled={!hasTemplate || isGenerating}>
                {isGenerating ? <Loader2 className="h-3 w-3 animate-spin" /> : <FileOutput className="h-3 w-3" />}
                {result ? "Re-generate" : "Generate"}
              </Button>
            </div>
          );
        })}
      </div>
    </div>
  );
}