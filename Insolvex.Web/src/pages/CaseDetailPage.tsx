import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { casesApi } from "@/services/api";
import { workflowApi } from "@/services/api/workflow";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CaseDto, CasePartyDto, DocumentDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import StageTimeline from "@/components/StageTimeline";
import CreditorMeetingModal from "@/components/CreditorMeetingModal";
import DocumentSigningPanel from "@/components/DocumentSigningPanel";
import {
  Loader2, FileText, Upload, Users, GitBranch, ChevronRight,
  Check, Clock, Ban, SkipForward, Brain, CalendarDays, RefreshCw,
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
  const [meetingOpen, setMeetingOpen] = useState(false);
  const [summary, setSummary] = useState<Record<string, unknown> | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<"overview" | "tasks" | "docs" | "parties" | "calendar">("overview");

  const load = () => {
    if (!id) return;
    Promise.all([
      casesApi.getById(id),
      casesApi.getParties(id),
      casesApi.getDocuments(id),
    ]).then(([caseRes, partiesRes, docsRes]) => {
      setCaseData(caseRes.data);
      setParties(partiesRes.data);
      setDocuments(docsRes.data);
    }).catch(console.error)
      .finally(() => setLoading(false));
  };

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
    try {
      await casesApi.advancePhase(id);
      load();
    } catch (err) { console.error(err); }
    finally { setAdvancing(false); }
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
              { id: "docs" as const, label: t.cases.documents, icon: FileText },
              { id: "parties" as const, label: "Parties", icon: Users },
              { id: "calendar" as const, label: "Calendar", icon: CalendarDays },
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
                  {hasPhases && currentPhase && (
                    <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5" onClick={handleAdvance} disabled={advancing}>
                      {advancing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <ChevronRight className="h-3.5 w-3.5" />}
                      Advance
                    </Button>
                  )}
                  {!hasPhases && (
                    <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5" onClick={handleInitPhases}>
                      Initialize Workflow
                    </Button>
                  )}
                </div>
                {hasPhases ? (
                  <div className="rounded-xl border border-border bg-card divide-y divide-border">
                    {phases.map((phase) => (
                      <div key={phase.id} className={`flex items-center gap-3 px-4 py-2.5 ${phase.status === "InProgress" ? "bg-primary/5" : ""}`}>
                        <div className="flex items-center justify-center w-6">
                          {phaseStatusIcon(phase.status)}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className={`text-sm font-medium ${phase.status === "Completed" ? "text-muted-foreground line-through" : phase.status === "InProgress" ? "text-primary" : "text-foreground"}`}>
                            {phaseLabel(phase.phaseType)}
                          </p>
                          {phase.courtDecisionRef && <p className="text-[10px] text-muted-foreground">{phase.courtDecisionRef}</p>}
                        </div>
                        <Badge variant={phaseStatusVariant(phase.status)} className="text-[10px] shrink-0">
                          {phaseStatusLabel(phase.status)}
                        </Badge>
                        {phase.dueDate && (
                          <span className="text-[10px] text-muted-foreground shrink-0">
                            {format(new Date(phase.dueDate), "dd MMM")}
                          </span>
                        )}
                      </div>
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

          {/* Documents Tab */}
          {activeTab === "docs" && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <FileText className="h-3.5 w-3.5" /> {t.cases.documents} ({documents.length})
                </h2>
                <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5">
                  <Upload className="h-3.5 w-3.5" />{t.cases.uploadDocument}
                </Button>
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
                        requiresSignature={(d as Record<string, unknown>).requiresSignature as boolean | undefined}
                        isSigned={(d as Record<string, unknown>).isSigned as boolean | undefined}
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
              <div className="rounded-xl border border-border bg-card divide-y divide-border">
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

          {/* Calendar Tab */}
          {activeTab === "calendar" && (
            <CalendarTab caseId={id!} />
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
              {e.location && <p className="text-[10px] text-muted-foreground">{e.location as string}</p>}
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
