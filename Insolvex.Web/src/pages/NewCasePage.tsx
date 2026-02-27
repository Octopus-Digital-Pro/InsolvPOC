import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { casesApi, companiesApi } from "@/services/api";
import { tribunalsApi } from "@/services/api/authorities";
import type { AuthorityRecord } from "@/services/api/authorities";
import { onrcApi } from "@/services/api/onrc";
import type { ONRCFirmResult } from "@/services/api/onrc";
import type { CompanyDto } from "@/services/api/types";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { Loader2, Search, ChevronDown, X, Building2, Gavel } from "lucide-react";

// ── Tribunal Combobox ────────────────────────────────────────────────────────

interface TribunalComboboxProps {
  tribunals: AuthorityRecord[];
  value: string;
  section: string;
  onChange: (name: string, section: string) => void;
}

function TribunalCombobox({ tribunals, value, section, onChange }: TribunalComboboxProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(value);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => { setQuery(value); }, [value]);

  const filtered = query.trim().length < 1
    ? tribunals
    : tribunals.filter(t =>
        t.name.toLowerCase().includes(query.toLowerCase()) ||
        (t.county ?? "").toLowerCase().includes(query.toLowerCase()) ||
        (t.section ?? "").toLowerCase().includes(query.toLowerCase())
      );

  const grouped = filtered.reduce<Record<string, AuthorityRecord[]>>((acc, t) => {
    const county = t.county ?? "Other";
    (acc[county] ??= []).push(t);
    return acc;
  }, {});

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const select = (t: AuthorityRecord) => {
    onChange(t.name, t.section ?? "");
    setQuery(t.name);
    setOpen(false);
  };

  const clear = (e: React.MouseEvent) => {
    e.stopPropagation();
    onChange("", "");
    setQuery("");
  };

  return (
    <div ref={containerRef} className="relative">
      <div className="relative flex items-center">
        <Gavel className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
        <input
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          placeholder="Search tribunals by name or county…"
          className="w-full rounded-md border border-input bg-background pl-8 pr-14 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <div className="absolute right-1 top-1/2 -translate-y-1/2 flex items-center gap-0.5">
          {value && (
            <button type="button" onClick={clear} className="p-1 text-muted-foreground hover:text-foreground rounded">
              <X className="h-3 w-3" />
            </button>
          )}
          <button type="button" onClick={() => setOpen(o => !o)}
            className="p-1 text-muted-foreground hover:text-foreground rounded">
            <ChevronDown className={`h-3.5 w-3.5 transition-transform ${open ? "rotate-180" : ""}`} />
          </button>
        </div>
      </div>

      {open && (
        <div className="absolute z-50 mt-1 w-full max-h-72 overflow-y-auto rounded-md border border-border bg-popover shadow-lg">
          {filtered.length === 0 ? (
            <p className="px-4 py-3 text-sm text-muted-foreground">No tribunals match "{query}"</p>
          ) : (
            Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b)).map(([county, items]) => (
              <div key={county}>
                <p className="px-3 pt-2 pb-0.5 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/60 bg-muted/40 sticky top-0">
                  {county}
                </p>
                {items.map(t => (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => select(t)}
                    className={`w-full text-left px-3 py-2 hover:bg-accent flex flex-col gap-0.5 transition-colors ${value === t.name ? "bg-primary/5 text-primary" : ""}`}
                  >
                    <span className="text-sm font-medium">{t.name}</span>
                    {t.section && <span className="text-[11px] text-muted-foreground">Section: {t.section}</span>}
                  </button>
                ))}
              </div>
            ))
          )}
        </div>
      )}

      {section && (
        <p className="mt-1 text-[11px] text-muted-foreground">
          Section: <span className="font-medium text-foreground">{section}</span>
        </p>
      )}
    </div>
  );
}

// ── Debtor Selector ──────────────────────────────────────────────────────────

interface SelectedDebtor {
  name: string;
  cui: string;
  source: "existing" | "onrc";
  companyId?: string;
}

function DebtorSelector({
  companies,
  onSelect,
}: {
  companies: CompanyDto[];
  onSelect: (name: string, cui: string, companyId?: string) => void;
}) {
  const [query, setQuery] = useState("");
  const [localResults, setLocalResults] = useState<CompanyDto[]>([]);
  const [_localLoading, setLocalLoading] = useState(false);
  const [onrcResults, setOnrcResults] = useState<ONRCFirmResult[]>([]);
  const [onrcLoading, setOnrcLoading] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [selected, setSelected] = useState<SelectedDebtor | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // Check for exact match locally — skip ONRC if found
  const hasExactMatch = localResults.some(c => {
    const q = query.trim().toLowerCase();
    return (c.cuiRo && c.cuiRo.toLowerCase() === q) || c.name.toLowerCase() === q;
  });

  // Also search inline from already-loaded companies for instant results
  const inlineMatched = query.trim().length >= 2
    ? companies.filter(c =>
        c.name.toLowerCase().includes(query.toLowerCase()) ||
        (c.cuiRo ?? "").toLowerCase().includes(query.toLowerCase())
      ).slice(0, 8)
    : [];

  // Merge inline + backend search results, deduplicate by id
  const matchedCompanies = (() => {
    const seen = new Set<string>();
    const merged: CompanyDto[] = [];
    for (const c of [...inlineMatched, ...localResults]) {
      if (!seen.has(c.id)) { seen.add(c.id); merged.push(c); }
    }
    return merged.slice(0, 10);
  })();

  // Search backend as user types (debounced)
  useEffect(() => {
    if (query.trim().length < 2) { setLocalResults([]); return; }
    const timer = setTimeout(() => {
      setLocalLoading(true);
      companiesApi.search(query.trim(), 10)
        .then(r => setLocalResults(r.data))
        .catch(console.error)
        .finally(() => setLocalLoading(false));
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setShowDropdown(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const handleQueryChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setQuery(e.target.value);
    setOnrcResults([]);
    setShowDropdown(true);
  };

  const searchOnrc = async () => {
    if (query.trim().length < 2 || hasExactMatch) return;
    setOnrcLoading(true);
    try {
      const r = await onrcApi.search(query.trim(), "Romania", 10);
      setOnrcResults(r.data);
      setShowDropdown(true);
    } catch (e) { console.error(e); }
    finally { setOnrcLoading(false); }
  };

  const selectExisting = (c: CompanyDto) => {
    const sel: SelectedDebtor = { name: c.name, cui: c.cuiRo ?? "", source: "existing", companyId: c.id };
    setSelected(sel);
    onSelect(c.name, c.cuiRo ?? "", c.id);
    setQuery(""); setOnrcResults([]); setLocalResults([]); setShowDropdown(false);
  };

  const selectOnrc = (r: ONRCFirmResult) => {
    const sel: SelectedDebtor = { name: r.name, cui: r.cui, source: "onrc" };
    setSelected(sel);
    onSelect(r.name, r.cui, undefined);
    setQuery(""); setOnrcResults([]); setLocalResults([]); setShowDropdown(false);
  };

  const clear = () => {
    setSelected(null);
    setQuery(""); setOnrcResults([]); setLocalResults([]); setShowDropdown(false);
    onSelect("", "", undefined);
  };

  if (selected) {
    return (
      <div className="flex items-center gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 dark:bg-emerald-950/20 dark:border-emerald-800 px-3 py-2.5">
        <Building2 className="h-4 w-4 text-emerald-600 shrink-0" />
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-foreground truncate">{selected.name}</p>
          <p className="text-[11px] text-muted-foreground">
            {selected.cui ? `CUI: ${selected.cui} · ` : ""}
            {selected.source === "existing" ? "Linked to existing company record" : "New company will be created from ONRC"}
          </p>
        </div>
        <button type="button" onClick={clear} className="shrink-0 text-muted-foreground hover:text-foreground">
          <X className="h-4 w-4" />
        </button>
      </div>
    );
  }

  const hasOnrcResults = onrcResults.length > 0;
  const hasExisting = matchedCompanies.length > 0;
  const showPanel = showDropdown && query.trim().length >= 2;

  return (
    <div ref={containerRef} className="space-y-2">
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
          <input
            value={query}
            onChange={handleQueryChange}
            onFocus={() => query.trim().length >= 2 && setShowDropdown(true)}
            onKeyDown={e => e.key === "Enter" && (e.preventDefault(), searchOnrc())}
            placeholder="Type debtor name or CUI…"
            className="w-full rounded-md border border-input bg-background pl-8 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <Button
          type="button"
          variant="outline"
          onClick={searchOnrc}
          disabled={onrcLoading || query.trim().length < 2 || hasExactMatch}
          className="shrink-0 gap-1.5 text-xs"
          title={hasExactMatch ? "Exact match found locally — ONRC search skipped" : "Search ONRC national registry"}
        >
          {onrcLoading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Search className="h-3.5 w-3.5" />}
          ONRC
        </Button>
      </div>

      {showPanel && (
        <div className="rounded-lg border border-border bg-popover shadow-md overflow-hidden">
          {hasExisting && (
            <>
              <p className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0">
                Existing companies
              </p>
              {matchedCompanies.map(c => (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => selectExisting(c)}
                  className="w-full text-left px-3 py-2.5 hover:bg-accent flex items-start gap-2.5 border-b border-border/30 last:border-0 transition-colors"
                >
                  <Building2 className="h-4 w-4 mt-0.5 shrink-0 text-emerald-500" />
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium truncate">{c.name}</p>
                    <p className="text-[11px] text-muted-foreground">
                      {c.cuiRo ? `CUI: ${c.cuiRo}` : "No CUI on record"}
                      {c.caseNumbers && c.caseNumbers.length > 0 ? ` · ${c.caseNumbers.length} case(s)` : ""}
                    </p>
                  </div>
                  <span className="ml-auto shrink-0 self-center text-[10px] px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400">
                    existing
                  </span>
                </button>
              ))}
            </>
          )}
          {hasOnrcResults && (
            <>
              <p className="px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0">
                ONRC Registry
              </p>
              {onrcResults.map(r => (
                <button
                  key={r.id}
                  type="button"
                  onClick={() => selectOnrc(r)}
                  className="w-full text-left px-3 py-2.5 hover:bg-accent flex items-start gap-2.5 border-b border-border/30 last:border-0 transition-colors"
                >
                  <Building2 className="h-4 w-4 mt-0.5 shrink-0 text-primary" />
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium truncate">{r.name}</p>
                    <p className="text-[11px] text-muted-foreground">
                      CUI: {r.cui}
                      {r.locality ? ` · ${r.locality}` : ""}
                      {r.county ? `, ${r.county}` : ""}
                      {r.tradeRegisterNo ? ` · ${r.tradeRegisterNo}` : ""}
                    </p>
                  </div>
                  {r.status && (
                    <span className={`ml-auto shrink-0 self-center text-[10px] px-1.5 py-0.5 rounded ${r.status.toUpperCase() === "ACTIV" ? "bg-primary/10 text-primary" : "bg-muted text-muted-foreground"}`}>
                      {r.status}
                    </span>
                  )}
                </button>
              ))}
            </>
          )}
          {!hasExisting && !hasOnrcResults && (
            <p className="px-3 py-3 text-sm text-muted-foreground">
              No existing companies match. Click <strong>Search ONRC</strong> to query the national registry.
            </p>
          )}
        </div>
      )}
    </div>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function NewCasePage() {
  const navigate = useNavigate();
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [tribunals, setTribunals] = useState<AuthorityRecord[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [caseNumber, setCaseNumber] = useState("");
  const [courtName, setCourtName] = useState("");
  const [courtSection, setCourtSection] = useState("");
  const [debtorName, setDebtorName] = useState("");
  const [debtorCui, setDebtorCui] = useState("");
  const [companyId, setCompanyId] = useState("");
  const [procedureType, setProcedureType] = useState("insolventa");
  const [lawReference, setLawReference] = useState("Legea 85/2014");

  // Key dates
  const [noticeDate, setNoticeDate] = useState("");
  const [openingDate, setOpeningDate] = useState("");
  const [nextHearingDate, setNextHearingDate] = useState("");
  const [claimsDeadline, setClaimsDeadline] = useState("");
  const [contestationsDeadline, setContestationsDeadline] = useState("");
  const [definitiveTableDate, setDefinitiveTableDate] = useState("");
  const [reorganizationPlanDeadline, setReorganizationPlanDeadline] = useState("");
  const [closureDate, setClosureDate] = useState("");

  useEffect(() => {
    companiesApi.getAll().then(r => setCompanies(r.data)).catch(console.error);
    tribunalsApi.getAll().then(r => setTribunals(r.data)).catch(console.error);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError("");
    try {
      const res = await casesApi.create({
        caseNumber,
        courtName: courtName || undefined,
        courtSection: courtSection || undefined,
        debtorName,
        debtorCui: debtorCui || undefined,
        companyId: companyId || undefined,
        procedureType: procedureType || undefined,
        lawReference: lawReference || undefined,
        noticeDate: noticeDate || undefined,
        openingDate: openingDate || undefined,
        nextHearingDate: nextHearingDate || undefined,
        claimsDeadline: claimsDeadline || undefined,
        contestationsDeadline: contestationsDeadline || undefined,
        definitiveTableDate: definitiveTableDate || undefined,
        reorganizationPlanDeadline: reorganizationPlanDeadline || undefined,
        closureDate: closureDate || undefined,
      } as Parameters<typeof casesApi.create>[0]);
      navigate(`/cases/${res.data.id}`);
    } catch (err) {
      const axErr = err as { response?: { data?: { message?: string } } };
      setError(axErr?.response?.data?.message ?? "Failed to create case.");
      console.error("Create case failed:", err);
    } finally {
      setSaving(false);
    }
  };

  const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground";

  return (
    <div className="mx-auto max-w-2xl">
      <BackButton onClick={() => navigate("/cases")}>Back to cases</BackButton>
      <h1 className="mt-2 text-xl font-bold text-foreground mb-5">New Insolvency Case</h1>

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Case identification */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Case Details</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={labelCls}>Case Number *</label>
              <input value={caseNumber} onChange={e => setCaseNumber(e.target.value)} required className={inputCls} placeholder="e.g. 1234/1285/2025" />
            </div>
            <div>
              <label className={labelCls}>Procedure Type</label>
              <select value={procedureType} onChange={e => setProcedureType(e.target.value)} className={inputCls}>
                <option value="insolventa">Insolvență generală (Legea 85/2014)</option>
                <option value="falimentSimplificat">Faliment simplificat</option>
                <option value="faliment">Faliment</option>
                <option value="reorganizare">Reorganizare judiciară</option>
                <option value="concordatPreventiv">Concordat preventiv</option>
                <option value="mandatAdHoc">Mandat ad-hoc</option>
                <option value="other">Other</option>
              </select>
            </div>
            <div className="sm:col-span-2">
              <label className={labelCls}>Law Reference</label>
              <input value={lawReference} onChange={e => setLawReference(e.target.value)} className={inputCls} />
            </div>
          </div>
        </div>

        {/* Court — tribunal selector */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Court</h2>
          <div>
            <label className={labelCls}>Tribunal</label>
            <TribunalCombobox
              tribunals={tribunals}
              value={courtName}
              section={courtSection}
              onChange={(name, sec) => { setCourtName(name); setCourtSection(sec); }}
            />
            <p className="mt-1.5 text-[11px] text-muted-foreground">
              Select from the list or type to search. Court section is auto-filled from the tribunal record.
            </p>
          </div>
          <div>
            <label className={labelCls}>
              Court Section
              <span className="ml-1 normal-case font-normal text-muted-foreground/70">(auto-filled, editable)</span>
            </label>
            <input value={courtSection} onChange={e => setCourtSection(e.target.value)} className={inputCls} placeholder="e.g. Secția a II-a Civilă" />
          </div>
        </div>

        {/* Debtor — search existing companies or ONRC */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Debtor</h2>
          <div>
            <label className={labelCls}>
              <span className="flex items-center gap-1.5">
                <Building2 className="h-3 w-3" />
                Search Debtor
                <span className="normal-case font-normal text-muted-foreground/70">
                  — matches existing companies instantly, or click Search ONRC
                </span>
              </span>
            </label>
            <DebtorSelector
              companies={companies}
              onSelect={(name, cui, cid) => {
                setDebtorName(name);
                setDebtorCui(cui);
                setCompanyId(cid ?? "");
              }}
            />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={labelCls}>Debtor Name *</label>
              <input value={debtorName} onChange={e => setDebtorName(e.target.value)} required className={inputCls} placeholder="e.g. SC Example SRL" />
            </div>
            <div>
              <label className={labelCls}>Debtor CUI</label>
              <input value={debtorCui} onChange={e => setDebtorCui(e.target.value)} className={inputCls} placeholder="e.g. RO12345678" />
            </div>
          </div>
        </div>

        {/* Key Dates */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Key Dates <span className="font-normal text-muted-foreground">(optional)</span></h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className={labelCls}>Notice Date</label>
              <input type="date" value={noticeDate} onChange={e => setNoticeDate(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Opening Date</label>
              <input type="date" value={openingDate} onChange={e => setOpeningDate(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Next Hearing</label>
              <input type="date" value={nextHearingDate} onChange={e => setNextHearingDate(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Claims Deadline</label>
              <input type="date" value={claimsDeadline} onChange={e => setClaimsDeadline(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Contestations Deadline</label>
              <input type="date" value={contestationsDeadline} onChange={e => setContestationsDeadline(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Definitive Table Date</label>
              <input type="date" value={definitiveTableDate} onChange={e => setDefinitiveTableDate(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Reorganization Plan Deadline</label>
              <input type="date" value={reorganizationPlanDeadline} onChange={e => setReorganizationPlanDeadline(e.target.value)} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Closure Date</label>
              <input type="date" value={closureDate} onChange={e => setClosureDate(e.target.value)} className={inputCls} />
            </div>
          </div>
        </div>

        {error && (
          <p className="rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-2.5 text-sm text-destructive">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="outline" onClick={() => navigate("/cases")}>Cancel</Button>
          <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="h-4 w-4 animate-spin mr-1.5" />}
            Create Case
          </Button>
        </div>
      </form>
    </div>
  );
}
