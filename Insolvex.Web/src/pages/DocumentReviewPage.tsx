import { useState, useEffect, useMemo, useCallback } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "@/contexts/LanguageContext";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { Loader2, FileText, Briefcase, FolderOpen, Sparkles, Users, CalendarDays, GitBranch, Trash2, Plus, RefreshCw, ChevronDown, ChevronRight } from "lucide-react";
import client from "@/services/api/client";
import { casesApi, tribunalsApi, companiesApi } from "@/services/api";
import type { CaseDto, UploadData, ExtractedParty, CompanyDto } from "@/services/api/types";
import type { AuthorityRecord } from "@/services/api/authorities";

const PROCEDURE_TYPES = [
  { value: "FalimentSimplificat", label: "Faliment Simplificat" },
  { value: "Faliment", label: "Faliment" },
  { value: "Insolventa", label: "Insolvență Generală" },
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

/** Strips the optional "RO" prefix from a CUI/CIF string, returning only the digits. */
const stripRoPrefix = (v?: string | null): string => v?.replace(/^RO/i, "").trim() ?? "";

/** Returns extra className tokens to visually highlight a field pre-filled by AI extraction. */
const aiField = (value: string, isAiExtracted: boolean): string =>
  isAiExtracted && value.trim() !== ""
    ? " border-primary/40 bg-primary/5 focus:ring-primary/60"
    : "";

export default function DocumentReviewPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillCaseId = searchParams.get("caseId");
  const { t, locale } = useTranslation();

  const [upload, setUpload] = useState<UploadData | null>(null);
  const [cases, setCases] = useState<CaseDto[]>([]);
  const [tribunals, setTribunals] = useState<AuthorityRecord[]>([]);
  const [debtorOptions, setDebtorOptions] = useState<CompanyDto[]>([]);

  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedCaseId, setSelectedCaseId] = useState<string>("");
  const [action, setAction] = useState<"newCase" | "filing">("newCase");
  const [showExtractedSummary, setShowExtractedSummary] = useState(false);

  const [selectedTribunalId, setSelectedTribunalId] = useState<string>("");
  const [selectedDebtorCompanyId, setSelectedDebtorCompanyId] = useState<string>("");

  const [caseNumber, setCaseNumber] = useState("");
  const [debtorName, setDebtorName] = useState("");
  const [debtorCui, setDebtorCui] = useState("");
  const [courtName, setCourtName] = useState("");
  const [courtSection, setCourtSection] = useState("");
  const [judgeSyndic, setJudgeSyndic] = useState("");
  const [registrar, setRegistrar] = useState("");
  const [procedureType, setProcedureType] = useState("Other");
  const [openingDate, setOpeningDate] = useState("");
  const [nextHearingDate, setNextHearingDate] = useState("");
  const [claimsDeadline, setClaimsDeadline] = useState("");
  const [contestationsDeadline, setContestationsDeadline] = useState("");
  const [parties, setParties] = useState<ExtractedParty[]>([]);

  useEffect(() => {
    if (!id) return;

    setLoading(true);
    Promise.all([
      client.get<UploadData>(`/documents/upload/${id}`),
      casesApi.getAll(),
      tribunalsApi.getAll(),
    ]).then(async ([uploadRes, casesRes, tribunalsRes]) => {
      const u = uploadRes.data;
      const tribunalList = tribunalsRes.data;

      setUpload(u);
      setCases(casesRes.data);
      setTribunals(tribunalList);

      if (prefillCaseId) {
        setAction("filing");
        setSelectedCaseId(prefillCaseId);
      } else {
        setAction(u.recommendedAction === "filing" ? "filing" : "newCase");
      }

      setCaseNumber(u.caseNumber ?? "");
      setDebtorName(u.debtorName ?? "");
      setDebtorCui(u.debtorCui ?? "");
      setCourtName(u.courtName ?? "");
      setCourtSection(u.courtSection ?? "");
      setJudgeSyndic(u.judgeSyndic ?? "");
      setRegistrar(u.registrar ?? "");
      setProcedureType(u.procedureType ?? "Other");
      setOpeningDate(u.openingDate ? u.openingDate.split("T")[0] : "");
      setNextHearingDate(u.nextHearingDate ? u.nextHearingDate.split("T")[0] : "");
      setClaimsDeadline(u.claimsDeadline ? u.claimsDeadline.split("T")[0] : "");
      setContestationsDeadline(u.contestationsDeadline ? u.contestationsDeadline.split("T")[0] : "");
      setParties(u.parties ?? []);

      if (u.matchedCaseId) setSelectedCaseId(u.matchedCaseId);
      if (u.matchedCompanyId) setSelectedDebtorCompanyId(u.matchedCompanyId);

      const tribunalMatch = tribunalList.find((x) =>
        x.name?.toLowerCase() === (u.courtName ?? "").toLowerCase() &&
        (x.section ?? "").toLowerCase() === (u.courtSection ?? "").toLowerCase()
      ) ?? tribunalList.find((x) =>
        (u.courtName ?? "").toLowerCase().includes((x.name ?? "").toLowerCase())
          || (x.name ?? "").toLowerCase().includes((u.courtName ?? "").toLowerCase())
      );

      if (tribunalMatch) setSelectedTribunalId(tribunalMatch.id);

      if (u.debtorName) {
        try {
          const searchRes = await companiesApi.search(u.debtorName, 20);
          setDebtorOptions(searchRes.data);
        } catch {
          setDebtorOptions([]);
        }
      }
    }).catch(console.error)
      .finally(() => setLoading(false));
  }, [id, prefillCaseId, refreshKey]);

  const handleReanalyze = useCallback(() => {
    setRefreshKey(k => k + 1);
  }, []);

  const handleSearchDebtorMatches = async () => {
    const query = debtorName.trim() || upload?.debtorName?.trim();
    if (!query) return;
    try {
      const res = await companiesApi.search(query, 20);
      setDebtorOptions(res.data);
    } catch (err) {
      console.error("Debtor search failed", err);
    }
  };

  const handleCreateDebtor = async () => {
    const namePrompt = locale === "ro" ? "Denumire companie debitor:" : locale === "hu" ? "Adós cég neve:" : "Debtor company name:";
    const cuiPrompt = locale === "ro" ? "CUI (opțional):" : locale === "hu" ? "CUI (opcionális):" : "CUI (optional):";

    const name = window.prompt(namePrompt, debtorName || upload?.debtorName || "");
    if (!name || !name.trim()) return;
    const cui = window.prompt(cuiPrompt, debtorCui || upload?.debtorCui || "") ?? "";

    try {
      const res = await companiesApi.create({
        name: name.trim(),
        cuiRo: cui.trim() || undefined,
      });

      setDebtorOptions((prev) => {
        const exists = prev.some((x) => x.id === res.data.id);
        return exists ? prev : [res.data, ...prev];
      });

      setSelectedDebtorCompanyId(res.data.id);
      setDebtorName(res.data.name);
      setDebtorCui(stripRoPrefix(res.data.cuiRo));
    } catch (err) {
      console.error("Create debtor failed", err);
    }
  };

  const handleCreateTribunal = async () => {
    const namePrompt = locale === "ro" ? "Denumire tribunal:" : locale === "hu" ? "Bíróság neve:" : "Tribunal name:";
    const sectionPrompt = locale === "ro" ? "Secție (opțional):" : locale === "hu" ? "Szekció (opcionális):" : "Section (optional):";

    const name = window.prompt(namePrompt, courtName || upload?.courtName || "");
    if (!name || !name.trim()) return;
    const section = window.prompt(sectionPrompt, courtSection || upload?.courtSection || "") ?? "";

    try {
      const res = await tribunalsApi.create({
        name: name.trim(),
        section: section.trim() || null,
        locality: null,
        county: null,
        address: null,
        postalCode: null,
        registryPhone: null,
        registryFax: null,
        registryEmail: null,
        registryHours: null,
        website: null,
        contactPerson: null,
        notes: null,
        overridesGlobalId: null,
      });

      const created = res.data as AuthorityRecord;
      setTribunals((prev) => [created, ...prev]);
      setSelectedTribunalId(created.id);
      setCourtName(created.name);
      setCourtSection(created.section ?? "");
    } catch (err) {
      console.error("Create tribunal failed", err);
    }
  };

  const handleConfirm = async () => {
    if (!id) return;

    setConfirming(true);
    try {
      const body: Record<string, unknown> = { action };
      if (action === "newCase") {
        body.caseNumber = caseNumber || undefined;
        body.debtorName = debtorName || undefined;
        body.debtorCui = debtorCui || undefined;
        body.courtName = courtName || undefined;
        body.courtSection = courtSection || undefined;
        body.judgeSyndic = judgeSyndic || undefined;
        body.registrar = registrar || undefined;
        body.procedureType = procedureType;
        body.openingDate = openingDate || undefined;
        body.nextHearingDate = nextHearingDate || undefined;
        body.claimsDeadline = claimsDeadline || undefined;
        body.contestationsDeadline = contestationsDeadline || undefined;
        body.parties = parties;
        body.companyId = selectedDebtorCompanyId || undefined;
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

  const selectedDebtor = useMemo(
    () => debtorOptions.find((x) => x.id === selectedDebtorCompanyId) ?? null,
    [debtorOptions, selectedDebtorCompanyId]
  );

  const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground";
  const isAi = upload?.isAiExtracted ?? false;

  useEffect(() => {
    if (!selectedDebtor) return;
    setDebtorName(selectedDebtor.name ?? "");
    // Prefer the company's stored CUI; fall back to the document-extracted CUI
    // so the AI analysis result is never silently cleared when the company record
    // has no CUI on file yet.
    setDebtorCui(stripRoPrefix(selectedDebtor.cuiRo) || upload?.debtorCui || "");
  }, [selectedDebtor, upload]);

  useEffect(() => {
    const tribunal = tribunals.find((x) => x.id === selectedTribunalId);
    if (!tribunal) return;
    setCourtName(tribunal.name ?? "");
    // Prefer the tribunal record's section; fall back to the AI-extracted section
    // so the document analysis result isn't cleared when the record has no section.
    setCourtSection(tribunal.section || upload?.courtSection || "");
  }, [selectedTribunalId, tribunals, upload]);

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!upload) return <p className="p-8 text-muted-foreground">{t.common.noResults}</p>;

  const confidencePct = Math.round(upload.confidence * 100);

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <BackButton onClick={() => navigate("/dashboard")}>{t.docReview.backToDashboard}</BackButton>

      <div className="rounded-xl border border-border bg-card p-5">
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10">
            <Sparkles className="h-6 w-6 text-primary" />
          </div>
          <div className="flex-1">
            <h1 className="text-xl font-bold text-foreground">{t.docReview.title}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t.docReview.subtitle}</p>
          </div>
          <Button variant="outline" size="sm" className="text-xs gap-1.5" onClick={handleReanalyze} disabled={loading}>
            {loading ? <Loader2 className="h-3 w-3 animate-spin" /> : <RefreshCw className="h-3 w-3" />}
            {loading ? t.common.loading ?? "Loading…" : "Re-analyze"}
          </Button>
          <Badge variant={confidencePct >= 80 ? "success" : "warning"} className="text-xs">
            {confidencePct}% {t.docReview.confidence}
          </Badge>
          <Badge variant={upload.isAiExtracted ? "success" : "outline"} className="text-xs ml-2">
            {upload.isAiExtracted ? t.docReview.aiExtracted : t.docReview.heuristicExtracted}
          </Badge>
        </div>
      </div>

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
            {cases.map(c => <option key={c.id} value={c.id}>{c.caseNumber} • {c.debtorName}</option>)}
          </select>
        </div>
      )}

      {action === "filing" && (upload.debtorName || upload.openingDate || upload.judgeSyndic || (upload.parties?.length ?? 0) > 0) && (
        <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
          <button
            type="button"
            className="flex w-full items-center justify-between text-xs font-semibold uppercase tracking-wide text-primary"
            onClick={() => setShowExtractedSummary(v => !v)}
          >
            <span className="flex items-center gap-1.5">
              <Sparkles className="h-3.5 w-3.5" /> Extracted Document Data
            </span>
            {showExtractedSummary ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
          </button>
          {showExtractedSummary && (
            <div className="mt-3 space-y-2 text-sm">
              {upload.debtorName && <div className="flex justify-between"><span className="text-muted-foreground">Debtor</span><span className="font-medium">{upload.debtorName}{upload.debtorCui ? ` (CUI: ${upload.debtorCui})` : ""}</span></div>}
              {upload.courtName && <div className="flex justify-between"><span className="text-muted-foreground">Court</span><span className="font-medium">{upload.courtName}{upload.courtSection ? ` — ${upload.courtSection}` : ""}</span></div>}
              {upload.judgeSyndic && <div className="flex justify-between"><span className="text-muted-foreground">Judge Syndic</span><span className="font-medium">{upload.judgeSyndic}</span></div>}
              {upload.registrar && <div className="flex justify-between"><span className="text-muted-foreground">Registrar</span><span className="font-medium">{upload.registrar}</span></div>}
              {upload.openingDate && <div className="flex justify-between"><span className="text-muted-foreground">Opening Date</span><span className="font-medium">{upload.openingDate.split("T")[0]}</span></div>}
              {upload.nextHearingDate && <div className="flex justify-between"><span className="text-muted-foreground">Next Hearing</span><span className="font-medium">{upload.nextHearingDate.split("T")[0]}</span></div>}
              {upload.claimsDeadline && <div className="flex justify-between"><span className="text-muted-foreground">Claims Deadline</span><span className="font-medium">{upload.claimsDeadline.split("T")[0]}</span></div>}
              {upload.contestationsDeadline && <div className="flex justify-between"><span className="text-muted-foreground">Contestations Deadline</span><span className="font-medium">{upload.contestationsDeadline.split("T")[0]}</span></div>}
              {(upload.parties?.length ?? 0) > 0 && (
                <div>
                  <p className="text-muted-foreground mb-1">Parties ({upload.parties.length})</p>
                  <div className="space-y-1">
                    {upload.parties.map((p, i) => (
                      <div key={i} className="flex justify-between text-xs">
                        <Badge variant="secondary" className="text-[10px]">{p.role}</Badge>
                        <span className="ml-2 font-medium truncate">{p.name}{p.fiscalId ? ` (${p.fiscalId})` : ""}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {action === "newCase" && (
        <>
          <div className="grid gap-5 lg:grid-cols-2">
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

            <div className="rounded-xl border border-border bg-card p-4 space-y-3">
              <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.docReview.newCaseDetails}</h2>
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <label className={labelCls}>{t.cases.caseNumber}</label>
                  <input value={caseNumber} onChange={e => setCaseNumber(e.target.value)} className={inputCls + aiField(caseNumber, isAi)} placeholder="1234/1285/2025" />
                </div>
                <div>
                  <label className={labelCls}>{t.cases.procedureType}</label>
                  <select value={procedureType} onChange={e => setProcedureType(e.target.value)} className={inputCls}>
                    {PROCEDURE_TYPES.map(pt => <option key={pt.value} value={pt.value}>{pt.label}</option>)}
                  </select>
                </div>

                <div className="sm:col-span-2 rounded-lg border border-border p-3 bg-muted/20 space-y-2">
                  <label className={labelCls}>{t.docReview.debtorSelection}</label>
                  <div className="flex gap-2">
                    <select
                      value={selectedDebtorCompanyId}
                      onChange={(e) => setSelectedDebtorCompanyId(e.target.value)}
                      className={inputCls}
                    >
                      <option value="">{t.docReview.chooseDebtor}</option>
                      {debtorOptions.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}{c.cuiRo ? ` (${c.cuiRo})` : ""}
                        </option>
                      ))}
                    </select>
                    <Button type="button" variant="outline" size="sm" onClick={handleSearchDebtorMatches}>{t.docReview.searchDebtorMatches}</Button>
                    <Button type="button" variant="outline" size="sm" onClick={handleCreateDebtor}>{t.docReview.createDebtor}</Button>
                  </div>
                </div>

                <div className="sm:col-span-2">
                  <label className={labelCls}>{t.cases.debtorName}</label>
                  <input value={debtorName} onChange={e => setDebtorName(e.target.value)} className={inputCls + aiField(debtorName, isAi)} />
                </div>

                <div>
                  <label className={labelCls}>{t.cases.debtorCui}</label>
                  <input value={debtorCui} onChange={e => setDebtorCui(stripRoPrefix(e.target.value))} className={inputCls + aiField(debtorCui, isAi)} placeholder="e.g. 12345678" />
                </div>

                <div className="sm:col-span-2 rounded-lg border border-border p-3 bg-muted/20 space-y-2">
                  <label className={labelCls}>{t.docReview.tribunalSelection}</label>
                  <div className="flex gap-2">
                    <select
                      value={selectedTribunalId}
                      onChange={(e) => setSelectedTribunalId(e.target.value)}
                      className={inputCls}
                    >
                      <option value="">{t.docReview.chooseTribunal}</option>
                      {tribunals.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.name}{x.section ? ` — ${x.section}` : ""}
                        </option>
                      ))}
                    </select>
                    <Button type="button" variant="outline" size="sm" onClick={handleCreateTribunal}>{t.docReview.createTribunal}</Button>
                  </div>
                </div>

                <div>
                  <label className={labelCls}>{t.cases.court}</label>
                  <input value={courtName} onChange={e => setCourtName(e.target.value)} className={inputCls + aiField(courtName, isAi)} />
                </div>
                <div>
                  <label className={labelCls}>{t.cases.courtSection}</label>
                  <input value={courtSection} onChange={e => setCourtSection(e.target.value)} className={inputCls + aiField(courtSection, isAi)} />
                </div>
                <div>
                  <label className={labelCls}>{t.cases.judgeSyndic}</label>
                  <input value={judgeSyndic} onChange={e => setJudgeSyndic(e.target.value)} className={inputCls + aiField(judgeSyndic, isAi)} />
                </div>
                <div>
                  <label className={labelCls}>{t.cases.registrar}</label>
                  <input value={registrar} onChange={e => setRegistrar(e.target.value)} className={inputCls + aiField(registrar, isAi)} />
                </div>
              </div>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-4">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
              <CalendarDays className="h-3.5 w-3.5" /> {t.docReview.keyDates}
              {isAi && (openingDate || nextHearingDate || claimsDeadline || contestationsDeadline) && (
                <Badge variant="success" className="text-[9px] ml-1"><Sparkles className="h-2.5 w-2.5 mr-0.5 inline" />AI</Badge>
              )}
            </h2>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <label className={labelCls}>{t.cases.openingDate}</label>
                <input type="date" value={openingDate} onChange={e => setOpeningDate(e.target.value)} className={inputCls + aiField(openingDate, isAi)} />
              </div>
              <div>
                <label className={labelCls}>{t.cases.nextHearing}</label>
                <input type="date" value={nextHearingDate} onChange={e => setNextHearingDate(e.target.value)} className={inputCls + aiField(nextHearingDate, isAi)} />
              </div>
              <div>
                <label className={labelCls}>{t.cases.claimsDeadline}</label>
                <input type="date" value={claimsDeadline} onChange={e => setClaimsDeadline(e.target.value)} className={inputCls + aiField(claimsDeadline, isAi)} />
              </div>
              <div>
                <label className={labelCls}>{t.docReview.contestationsDeadline}</label>
                <input type="date" value={contestationsDeadline} onChange={e => setContestationsDeadline(e.target.value)} className={inputCls + aiField(contestationsDeadline, isAi)} />
              </div>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-4">
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
                <Users className="h-3.5 w-3.5" /> {t.docReview.partiesTitle} ({parties.length})
                {isAi && parties.length > 0 && (
                  <Badge variant="success" className="text-[9px] ml-1"><Sparkles className="h-2.5 w-2.5 mr-0.5 inline" />AI</Badge>
                )}
              </h2>
              <Button variant="outline" size="sm" className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5" onClick={addParty}>
                <Plus className="h-3 w-3" /> {t.docReview.addParty}
              </Button>
            </div>
            {parties.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-4">{t.docReview.noPartiesDetected}</p>
            ) : (
              <div className="space-y-2">
                {parties.map((p, idx) => (
                  <div key={idx} className="flex items-start gap-2 rounded-lg border border-border p-2.5 bg-background">
                    <div className="grid gap-2 sm:grid-cols-4 flex-1">
                      <div>
                        <label className={labelCls}>{t.docReview.role}</label>
                        <select value={p.role} onChange={e => updateParty(idx, "role", e.target.value)} className={inputCls}>
                          {PARTY_ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                        </select>
                      </div>
                      <div className="sm:col-span-2">
                        <label className={labelCls}>{t.docReview.partyName}</label>
                        <input value={p.name} onChange={e => updateParty(idx, "name", e.target.value)} className={inputCls} />
                      </div>
                      <div>
                        <label className={labelCls}>{t.docReview.fiscalId}</label>
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

          <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
            <h2 className="text-xs font-semibold uppercase tracking-wide text-primary flex items-center gap-1.5 mb-2">
              <GitBranch className="h-3.5 w-3.5" /> {t.docReview.whatWillBeCreated}
            </h2>
            <div className="flex flex-wrap gap-3 text-xs text-foreground">
              <Badge variant="secondary">1 Case</Badge>
              <Badge variant="secondary">1 Document</Badge>
              <Badge variant="secondary">{parties.length} {parties.length === 1 ? "Party" : "Parties"}</Badge>
              <Badge variant="secondary">{parties.filter(p => p.role === "Debtor" || p.role === "Court" || p.role === "BudgetaryCreditor" || p.role === "InsolvencyPractitioner").length > 0 ? t.docReview.companiesAutoCreated : t.docReview.noNewCompanies}</Badge>
              <Badge variant="secondary">Workflow</Badge>
              <Badge variant="secondary">Tasks + reminders</Badge>
              <Badge variant="secondary">Scheduled emails</Badge>
            </div>
          </div>
        </>
      )}

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
