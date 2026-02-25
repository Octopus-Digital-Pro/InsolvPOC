import { useState, useEffect, useRef, useCallback } from "react";
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

// ── ONRC Debtor Search ───────────────────────────────────────────────────────

function ONRCDebtorSearch({ onSelect }: { onSelect: (name: string, cui: string) => void }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ONRCFirmResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const doSearch = useCallback((q: string) => {
    if (q.trim().length < 2) { setResults([]); setOpen(false); return; }
    setLoading(true);
    onrcApi.search(q, "Romania", 8)
      .then(r => { setResults(r.data); setOpen(r.data.length > 0); })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const q = e.target.value;
    setQuery(q);
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => doSearch(q), 380);
  };

  const select = (r: ONRCFirmResult) => {
    onSelect(r.name, r.cui);
    setQuery("");
    setResults([]);
    setOpen(false);
  };

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
        {loading
          ? <Loader2 className="absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 animate-spin text-muted-foreground" />
          : query && (
            <button type="button" onClick={() => { setQuery(""); setResults([]); setOpen(false); }}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground">
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        <input
          value={query}
          onChange={handleChange}
          onFocus={() => results.length > 0 && setOpen(true)}
          placeholder="Search national registry by name or CUI…"
          className="w-full rounded-md border border-dashed border-primary/50 bg-primary/5 pl-8 pr-8 py-2 text-sm placeholder:text-muted-foreground/60 focus:outline-none focus:ring-2 focus:ring-ring focus:border-solid"
        />
      </div>

      {open && results.length > 0 && (
        <div className="absolute z-50 mt-1 w-full max-h-60 overflow-y-auto rounded-md border border-border bg-popover shadow-lg">
          {results.map(r => (
            <button
              key={r.id}
              type="button"
              onClick={() => select(r)}
              className="w-full text-left px-3 py-2.5 hover:bg-accent flex items-start gap-2.5 border-b border-border/40 last:border-0"
            >
              <Building2 className="h-4 w-4 mt-0.5 shrink-0 text-muted-foreground" />
              <div className="min-w-0">
                <p className="text-sm font-medium truncate">{r.name}</p>
                <p className="text-[11px] text-muted-foreground">
                  CUI: {r.cui}
                  {r.locality ? ` · ${r.locality}` : ""}
                  {r.county ? `, ${r.county}` : ""}
                  {r.tradeRegisterNo ? ` · ${r.tradeRegisterNo}` : ""}
                </p>
              </div>
            </button>
          ))}
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

        {/* Debtor — ONRC lookup */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Debtor</h2>
          <div>
            <label className={labelCls}>
              <span className="flex items-center gap-1.5">
                <Building2 className="h-3 w-3" />
                ONRC Lookup
                <span className="normal-case font-normal text-muted-foreground/70">— search national registry to auto-fill</span>
              </span>
            </label>
            <ONRCDebtorSearch onSelect={(name, cui) => { setDebtorName(name); setDebtorCui(cui); }} />
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

        {/* Attachment */}
        <div className="rounded-xl border border-border bg-card p-5 space-y-3">
          <h2 className="text-sm font-semibold text-foreground">Attachment</h2>
          <div>
            <label className={labelCls}>Attach to Company</label>
            <select value={companyId} onChange={e => setCompanyId(e.target.value)} className={inputCls}>
              <option value="">— None (create standalone) —</option>
              {companies.map(c => (
                <option key={c.id} value={c.id}>{c.name}{c.cuiRo ? ` (${c.cuiRo})` : ""}</option>
              ))}
            </select>
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
