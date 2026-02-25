import { useState, useEffect } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "@/contexts/LanguageContext";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { Loader2, FileText, Briefcase, FolderOpen, Sparkles, Users, CalendarDays, GitBranch, Trash2, Plus } from "lucide-react";
import client from "@/services/api/client";
import { casesApi } from "@/services/api";
import type { CaseDto, UploadData, ExtractedParty } from "@/services/api/types";

const PROCEDURE_TYPES = [
  { value: "FalimentSimplificat", label: "Faliment Simplificat" },
  { value: "Faliment", label: "Faliment" },
  { value: "Insolventa", label: "Insolven?? General?" },
  { value: "Reorganizare", label: "Reorganizare" },
  { value: "ConcordatPreventiv", label: "Concordat Preventiv" },
  { value: "MandatAdHoc", label: "Mandat Ad-Hoc" },
  { value: "Other", label: "Altul" },
];

const PARTY_ROLES = [
  "Debtor", "InsolvencyPractitioner", "Court",
  "SecuredCreditor", "UnsecuredCreditor", "BudgetaryCreditor",
  "EmployeeCreditor", "JudgeSyndic", "CourtExpert",
  "CreditorsCommittee", "SpecialAdministrator", "Guarantor", "ThirdParty",
];

export default function DocumentReviewPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillCaseId = searchParams.get("caseId");
  const { t } = useTranslation();
  const [upload, setUpload] = useState<UploadData | null>(null);
  const [cases, setCases] = useState<CaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [selectedCaseId, setSelectedCaseId] = useState<string>("");
  const [action, setAction] = useState<"newCase" | "filing">("newCase");

// Editable fields
  const [caseNumber, setCaseNumber] = useState("");
  const [debtorName, setDebtorName] = useState("");
  const [courtName, setCourtName] = useState("");
  const [courtSection, setCourtSection] = useState("");
  const [judgeSyndic, setJudgeSyndic] = useState("");
  const [procedureType, setProcedureType] = useState("Other");
  const [openingDate, setOpeningDate] = useState("");
  const [nextHearingDate, setNextHearingDate] = useState("");
  const [claimsDeadline, setClaimsDeadline] = useState("");
  const [contestationsDeadline, setContestationsDeadline] = useState("");
  const [parties, setParties] = useState<ExtractedParty[]>([]);

  useEffect(() => {
    if (!id) return;
    Promise.all([
 client.get<UploadData>(`/documents/upload/${id}`),
      casesApi.getAll(),
    ]).then(([uploadRes, casesRes]) => {
      const u = uploadRes.data;
    setUpload(u);
   setCases(casesRes.data);
      // If a caseId was passed via URL, pre-select "file to existing"
      if (prefillCaseId) {
        setAction("filing");
        setSelectedCaseId(prefillCaseId);
      } else {
        setAction(u.recommendedAction === "filing" ? "filing" : "newCase");
      }
      setCaseNumber(u.caseNumber ?? "");
      setDebtorName(u.debtorName ?? "");
      setCourtName(u.courtName ?? "");
    setCourtSection(u.courtSection ?? "");
      setJudgeSyndic(u.judgeSyndic ?? "");
      setProcedureType(u.procedureType ?? "Other");
      setOpeningDate(u.openingDate ? u.openingDate.split("T")[0] : "");
      setNextHearingDate(u.nextHearingDate ? u.nextHearingDate.split("T")[0] : "");
      setClaimsDeadline(u.claimsDeadline ? u.claimsDeadline.split("T")[0] : "");
      setContestationsDeadline(u.contestationsDeadline ? u.contestationsDeadline.split("T")[0] : "");
      setParties(u.parties ?? []);
      if (u.matchedCaseId) setSelectedCaseId(u.matchedCaseId);
    }).catch(console.error)
    .finally(() => setLoading(false));
  }, [id]);

  const handleConfirm = async () => {
    if (!id) return;
    setConfirming(true);
    try {
      const body: Record<string, unknown> = { action };
 if (action === "newCase") {
        body.caseNumber = caseNumber || undefined;
        body.debtorName = debtorName || undefined;
        body.courtName = courtName || undefined;
    body.courtSection = courtSection || undefined;
        body.judgeSyndic = judgeSyndic || undefined;
        body.procedureType = procedureType;
        body.openingDate = openingDate || undefined;
        body.nextHearingDate = nextHearingDate || undefined;
        body.claimsDeadline = claimsDeadline || undefined;
        body.contestationsDeadline = contestationsDeadline || undefined;
   body.parties = parties;
 } else {
        body.caseId = selectedCaseId;
      }
    const res = await client.post<{ caseId: string }>(`/documents/upload/${id}/confirm`, body);
      navigate(`/cases/${res.data.caseId}`);
    } catch (err) {
      console.error("Confirm failed:", err);
    } finally {
      setConfirming(false);
    }
  };

  const addParty = () => setParties([...parties, { role: "UnsecuredCreditor", name: "", fiscalId: null, claimAmount: null }]);
  const removeParty = (idx: number) => setParties(parties.filter((_, i) => i !== idx));
  const updateParty = (idx: number, field: keyof ExtractedParty, value: string | number | null) => {
    setParties(parties.map((p, i) => i === idx ? { ...p, [field]: value } : p));
  };

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!upload) return <p className="p-8 text-muted-foreground">{t.common.noResults}</p>;

  const confidencePct = Math.round(upload.confidence * 100);
  const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground";

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <BackButton onClick={() => navigate("/dashboard")}>{t.docReview.backToDashboard}</BackButton>

      {/* Header */}
      <div className="rounded-xl border border-border bg-card p-5">
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10">
        <Sparkles className="h-6 w-6 text-primary" />
          </div>
 <div className="flex-1">
     <h1 className="text-xl font-bold text-foreground">{t.docReview.title}</h1>
         <p className="mt-1 text-sm text-muted-foreground">{t.docReview.subtitle}</p>
   </div>
          <Badge variant={confidencePct >= 70 ? "success" : "warning"} className="text-xs">
   {confidencePct}% {t.docReview.confidence}
  </Badge>
    </div>
      </div>

 {/* Action selector */}
      <div className="rounded-xl border border-border bg-card p-4">
        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.docReview.recommendedAction}</h2>
        <div className="flex gap-2">
          <label
   className={`flex-1 flex items-center gap-3 rounded-lg border p-3 cursor-pointer transition-all ${action === "newCase" ? "border-primary bg-primary/5" : "border-border hover:border-primary/30"}`}
            onClick={() => setAction("newCase")}
          >
            <input type="radio" name="action" checked={action === "newCase"} onChange={() => setAction("newCase")} className="mt-0.5" />
            <Briefcase className="h-4 w-4 text-primary shrink-0" />
      <div>
      <span className="text-sm font-semibold text-foreground">{t.docReview.createNewCase}</span>
 <p className="text-[10px] text-muted-foreground">{t.docReview.createNewCaseDesc}</p>
          </div>
          </label>
          <label
    className={`flex-1 flex items-center gap-3 rounded-lg border p-3 cursor-pointer transition-all ${action === "filing" ? "border-primary bg-primary/5" : "border-border hover:border-primary/30"}`}
          onClick={() => setAction("filing")}
       >
            <input type="radio" name="action" checked={action === "filing"} onChange={() => setAction("filing")} className="mt-0.5" />
            <FolderOpen className="h-4 w-4 text-chart-2 shrink-0" />
          <div>
  <span className="text-sm font-semibold text-foreground">{t.docReview.fileToExisting}</span>
              <p className="text-[10px] text-muted-foreground">{t.docReview.fileToExistingDesc}</p>
            </div>
          </label>
    </div>
  </div>

      {action === "filing" && (
        <div className="rounded-xl border border-border bg-card p-4">
          <label className={labelCls}>{t.docReview.selectCase}</label>
          <select value={selectedCaseId} onChange={e => setSelectedCaseId(e.target.value)} className={inputCls}>
 <option value="">{t.docReview.chooseCase}</option>
            {cases.map(c => <option key={c.id} value={c.id}>{c.caseNumber} � {c.debtorName}</option>)}
  </select>
        </div>
    )}

      {action === "newCase" && (
        <>
  {/* Document info + Case details side by side */}
          <div className="grid gap-5 lg:grid-cols-2">
        {/* Left: Document info */}
      <div className="rounded-xl border border-border bg-card p-4">
              <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
   <FileText className="h-3.5 w-3.5" /> {t.docReview.documentDetails}
           </h2>
         <div className="space-y-2 text-sm">
           <div className="flex justify-between"><span className="text-muted-foreground">{t.docReview.file}</span><span className="font-medium text-foreground truncate ml-2">{upload.fileName}</span></div>
      <div className="flex justify-between"><span className="text-muted-foreground">{t.docReview.size}</span><span>{(upload.fileSize / 1024).toFixed(0)} KB</span></div>
         <div className="flex justify-between"><span className="text-muted-foreground">{t.docReview.type}</span><Badge variant="secondary" className="text-[10px]">{upload.docType ?? t.common.unknown}</Badge></div>
              </div>
     {upload.extractedText && (
          <div className="mt-3">
                <h3 className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t.docReview.extractedText}</h3>
      <pre className="max-h-40 overflow-y-auto rounded-lg bg-muted/50 p-2 text-[10px] text-foreground whitespace-pre-wrap font-mono">{upload.extractedText}</pre>
       </div>
              )}
         </div>

          {/* Right: Case details form */}
      <div className="rounded-xl border border-border bg-card p-4 space-y-3">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.docReview.newCaseDetails}</h2>
              <div className="grid gap-3 sm:grid-cols-2">
      <div>
         <label className={labelCls}>{t.cases.caseNumber}</label>
     <input value={caseNumber} onChange={e => setCaseNumber(e.target.value)} className={inputCls} placeholder="1234/1285/2025" />
      </div>
    <div>
         <label className={labelCls}>{t.cases.procedureType}</label>
             <select value={procedureType} onChange={e => setProcedureType(e.target.value)} className={inputCls}>
  {PROCEDURE_TYPES.map(pt => <option key={pt.value} value={pt.value}>{pt.label}</option>)}
        </select>
    </div>
      <div className="sm:col-span-2">
      <label className={labelCls}>{t.cases.debtorName}</label>
         <input value={debtorName} onChange={e => setDebtorName(e.target.value)} className={inputCls} />
                </div>
   <div>
    <label className={labelCls}>{t.cases.court}</label>
     <input value={courtName} onChange={e => setCourtName(e.target.value)} className={inputCls} />
     </div>
        <div>
  <label className={labelCls}>{t.cases.courtSection}</label>
           <input value={courtSection} onChange={e => setCourtSection(e.target.value)} className={inputCls} />
       </div>
            <div>
        <label className={labelCls}>{t.cases.judgeSyndic}</label>
        <input value={judgeSyndic} onChange={e => setJudgeSyndic(e.target.value)} className={inputCls} />
 </div>
   </div>
            </div>
          </div>

          {/* Dates */}
          <div className="rounded-xl border border-border bg-card p-4">
       <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
         <CalendarDays className="h-3.5 w-3.5" /> Key Dates
            </h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div>
    <label className={labelCls}>{t.cases.openingDate}</label>
        <input type="date" value={openingDate} onChange={e => setOpeningDate(e.target.value)} className={inputCls} />
            </div>
    <div>
           <label className={labelCls}>{t.cases.nextHearing}</label>
       <input type="date" value={nextHearingDate} onChange={e => setNextHearingDate(e.target.value)} className={inputCls} />
        </div>
            <div>
  <label className={labelCls}>{t.cases.claimsDeadline}</label>
           <input type="date" value={claimsDeadline} onChange={e => setClaimsDeadline(e.target.value)} className={inputCls} />
        </div>
              <div>
       <label className={labelCls}>Contestations Deadline</label>
    <input type="date" value={contestationsDeadline} onChange={e => setContestationsDeadline(e.target.value)} className={inputCls} />
        </div>
            </div>
          </div>

   {/* Parties */}
          <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center justify-between mb-3">
  <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
       <Users className="h-3.5 w-3.5" /> Parties ({parties.length})
              </h2>
            <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5" onClick={addParty}>
       <Plus className="h-3 w-3" /> Add Party
              </Button>
      </div>
        {parties.length === 0 ? (
   <p className="text-sm text-muted-foreground text-center py-4">No parties detected. Add parties manually or they'll be auto-created from the debtor name.</p>
            ) : (
     <div className="space-y-2">
      {parties.map((p, idx) => (
             <div key={idx} className="flex items-start gap-2 rounded-lg border border-border p-2.5 bg-background">
      <div className="grid gap-2 sm:grid-cols-4 flex-1">
            <div>
        <label className={labelCls}>Role</label>
       <select value={p.role} onChange={e => updateParty(idx, "role", e.target.value)} className={inputCls}>
      {PARTY_ROLES.map(r => <option key={r} value={r}>{r}</option>)}
 </select>
                </div>
        <div className="sm:col-span-2">
      <label className={labelCls}>Name</label>
          <input value={p.name} onChange={e => updateParty(idx, "name", e.target.value)} className={inputCls} />
</div>
    <div>
          <label className={labelCls}>CUI / Fiscal ID</label>
         <input value={p.fiscalId ?? ""} onChange={e => updateParty(idx, "fiscalId", e.target.value || null)} className={inputCls} placeholder="RO12345678" />
       </div>
      </div>
      <Button variant="ghost" size="icon" className="h-8 w-8 shrink-0 mt-5" onClick={() => removeParty(idx)}>
    <Trash2 className="h-3.5 w-3.5 text-destructive" />
   </Button>
       </div>
           ))}
              </div>
       )}
          </div>

          {/* Summary: what will be created */}
          <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-primary flex items-center gap-1.5 mb-2">
  <GitBranch className="h-3.5 w-3.5" /> What will be created
       </h2>
        <div className="flex flex-wrap gap-3 text-xs text-foreground">
              <Badge variant="secondary">1 Case</Badge>
      <Badge variant="secondary">1 Document</Badge>
    <Badge variant="secondary">{parties.length} {parties.length === 1 ? "Party" : "Parties"}</Badge>
              <Badge variant="secondary">{parties.filter(p => p.role === "Debtor" || p.role === "Court" || p.role === "BudgetaryCreditor" || p.role === "InsolvencyPractitioner").length > 0 ? "Companies auto-created" : "No new companies"}</Badge>
    <Badge variant="secondary">Workflow phases</Badge>
  <Badge variant="secondary">Tasks + reminders</Badge>
    <Badge variant="secondary">Scheduled emails</Badge>
 </div>
          </div>
        </>
      )}

      {/* Confirm button */}
      <Button
        className="w-full bg-primary hover:bg-primary/90"
      size="lg"
     onClick={handleConfirm}
        disabled={confirming || (action === "filing" && !selectedCaseId)}
      >
        {confirming && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
        {action === "newCase" ? t.docReview.createAndAttach : t.docReview.fileToSelected}
      </Button>
    </div>
  );
}
