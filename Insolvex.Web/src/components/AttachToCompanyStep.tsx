import { useState, useMemo } from "react";
import type { Company } from "../types";
import type { InsolvencyExtractionResult } from "../services/openai";
import { normalizeForMatch, suggestCompanies } from "../services/companyMatch";
import CreateCompanyForm from "./molecules/CreateCompanyForm";
import SuggestedMatchCard from "./molecules/SuggestedMatchCard";

interface AttachToCompanyStepProps {
  caseSummary: { caseNumber: string; debtorName: string };
  extractionResult: InsolvencyExtractionResult;
  companies: Company[];
  suggestedCompanyId?: string | null;
  onCreateCompany: (company: Company) => Promise<void>;
  onAttach: (companyId: string) => void;
  onCancel: () => void;
  createdBy: string;
  sourceFileName: string;
}

export default function AttachToCompanyStep({
  caseSummary,
  extractionResult,
  companies,
  suggestedCompanyId = null,
  onCreateCompany,
  onAttach,
  onCancel,
  createdBy,
  sourceFileName,
}: AttachToCompanyStepProps) {
  const suggestedCompany = suggestedCompanyId
    ? companies.find((c) => c.id === suggestedCompanyId) ?? null
    : null;
  const [mode, setMode] = useState<"select" | "create">("select");
  const [search, setSearch] = useState("");
  const debtor = extractionResult.parties?.debtor;
  const [createName, setCreateName] = useState(debtor?.name && debtor.name !== "Not found" ? debtor.name : "");
  const [createCuiRo, setCreateCuiRo] = useState(debtor?.cui && debtor.cui !== "Not found" ? debtor.cui : "");
  const [createAddress, setCreateAddress] = useState(debtor?.address && debtor.address !== "Not found" ? debtor.address : "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const suggested = useMemo(() => {
    const name = debtor?.name ?? "";
    const identifiers = debtor?.cui ?? "";
    return suggestCompanies(companies, name, identifiers);
  }, [companies, debtor?.name, debtor?.cui]);

  const filteredCompanies = useMemo(() => {
    if (!search.trim()) return companies;
    const q = normalizeForMatch(search);
    return companies.filter((c) => normalizeForMatch(c.name).includes(q) || (c.cuiRo && c.cuiRo.toLowerCase().includes(q)));
  }, [companies, search]);

  const prefillFromDebtor = () => {
    if (debtor?.name && debtor.name !== "Not found") setCreateName(debtor.name);
    if (debtor?.address && debtor.address !== "Not found") setCreateAddress(debtor.address);
    if (debtor?.cui && debtor.cui !== "Not found") setCreateCuiRo(debtor.cui);
  };

  const handleCreateNew = () => {
    setMode("create");
    prefillFromDebtor();
  };

  const handleAttachExisting = (companyId: string) => {
    onAttach(companyId);
  };

  const handleSaveNewCompany = async () => {
    setError(null);
    const name = createName.trim();
    if (!name) {
      setError("Company name is required.");
      return;
    }
    setSaving(true);
    try {
      const company: Company = {
        id: crypto.randomUUID(),
        name,
        cuiRo: createCuiRo.trim(),
        address: createAddress.trim(),
        createdAt: new Date().toISOString(),
        createdBy,
      };
      await onCreateCompany(company);
      onAttach(company.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create company");
    } finally {
      setSaving(false);
    }
  };

  if (mode === "create") {
    return (
      <CreateCompanyForm
        name={createName}
        cuiRo={createCuiRo}
        address={createAddress}
        error={error}
        saving={saving}
        onPrefillDebtor={prefillFromDebtor}
        onNameChange={setCreateName}
        onCuiRoChange={setCreateCuiRo}
        onAddressChange={setCreateAddress}
        onSave={handleSaveNewCompany}
        onBack={() => setMode("select")}
      />
    );
  }

  return (
    <div className="mx-auto max-w-xl pt-12">
      <h2 className="text-xl font-semibold text-foreground">Attach to company</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Choose the debtor company or create a new one for this insolvency case.
      </p>
      <p className="mt-0.5 text-xs text-muted-foreground truncate" title={sourceFileName}>
        Case: {caseSummary.caseNumber} – {caseSummary.debtorName || "—"}
      </p>
      <p className="mt-0.5 text-xs text-muted-foreground truncate">File: {sourceFileName}</p>

      {suggestedCompany && (
        <SuggestedMatchCard
          company={suggestedCompany}
          onAttach={handleAttachExisting}
        />
      )}

      <div className="mt-6">
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search companies by name or CUI…"
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </div>

      {suggested.length > 0 && !search.trim() && (
        <section className="mt-6">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Suggested</h3>
          <ul className="space-y-2">
            {suggested.slice(0, 5).map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  onClick={() => handleAttachExisting(c.id)}
                  className="w-full rounded-xl border border-border bg-card px-4 py-3 text-left text-sm transition-colors hover:border-border hover:bg-accent"
                >
                  <span className="font-medium text-foreground">{c.name}</span>
                  {c.cuiRo && <span className="ml-2 text-xs text-muted-foreground">{c.cuiRo}</span>}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mt-6">
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {search.trim() ? "Search results" : "All companies"}
        </h3>
        {filteredCompanies.length === 0 ? (
          <p className="py-4 text-sm text-muted-foreground">No companies match.</p>
        ) : (
          <ul className="space-y-2 max-h-60 overflow-y-auto">
            {filteredCompanies.map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  onClick={() => handleAttachExisting(c.id)}
                  className="w-full rounded-xl border border-border bg-card px-4 py-3 text-left text-sm transition-colors hover:border-border hover:bg-accent"
                >
                  <span className="font-medium text-foreground">{c.name}</span>
                  {c.cuiRo && <span className="ml-2 text-xs text-muted-foreground">{c.cuiRo}</span>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="mt-6 pt-4 border-t border-border">
        <button
          type="button"
          onClick={handleCreateNew}
          className="rounded-md border border-dashed border-input px-4 py-3 text-sm font-medium text-muted-foreground hover:border-primary hover:bg-accent hover:text-primary"
        >
          + Create new company
        </button>
      </div>

      <div className="mt-6">
        <button
          type="button"
          onClick={onCancel}
          className="text-sm text-muted-foreground hover:text-foreground"
        >
          Cancel (discard scan)
        </button>
      </div>
    </div>
  );
}
