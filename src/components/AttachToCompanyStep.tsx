import { useState, useMemo } from "react";
import type { Company, ContractCase } from "../types";
import type { ContractExtractionResult } from "../services/openai";
import { normalizeForMatch, suggestCompanies } from "../services/companyMatch";
import CreateCompanyForm from "./molecules/CreateCompanyForm";
import SuggestedMatchCard from "./molecules/SuggestedMatchCard";

interface AttachToCompanyStepProps {
  draftCase: ContractCase;
  extractionResult: ContractExtractionResult;
  companies: Company[];
  suggestedCompanyId?: string | null;
  onCreateCompany: (company: Company) => Promise<void>;
  onAttach: (companyId: string) => void;
  onCancel: () => void;
  createdBy: string;
}

type PrefillSource = "beneficiary" | "contractor";

export default function AttachToCompanyStep({
  draftCase,
  extractionResult,
  companies,
  suggestedCompanyId = null,
  onCreateCompany,
  onAttach,
  onCancel,
  createdBy,
}: AttachToCompanyStepProps) {
  const suggestedCompany = suggestedCompanyId
    ? companies.find((c) => c.id === suggestedCompanyId) ?? null
    : null;
  const [mode, setMode] = useState<"select" | "create">("select");
  const [prefillSource, setPrefillSource] = useState<PrefillSource>("beneficiary");
  const [search, setSearch] = useState("");
  const [createName, setCreateName] = useState("");
  const [createCuiRo, setCreateCuiRo] = useState("");
  const [createAddress, setCreateAddress] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const suggested = useMemo(() => {
    const name = prefillSource === "beneficiary" ? extractionResult.beneficiary : extractionResult.contractor;
    const identifiers = prefillSource === "beneficiary" ? extractionResult.beneficiaryIdentifiers : extractionResult.contractorIdentifiers;
    return suggestCompanies(companies, name, identifiers);
  }, [companies, extractionResult, prefillSource]);

  const filteredCompanies = useMemo(() => {
    if (!search.trim()) return companies;
    const q = normalizeForMatch(search);
    return companies.filter((c) => normalizeForMatch(c.name).includes(q) || (c.cuiRo && c.cuiRo.toLowerCase().includes(q)));
  }, [companies, search]);

  const prefillFromScan = (source: PrefillSource) => {
    setPrefillSource(source);
    const name = source === "beneficiary" ? extractionResult.beneficiary : extractionResult.contractor;
    const address = source === "beneficiary" ? extractionResult.beneficiaryAddress : extractionResult.contractorAddress;
    const identifiers = source === "beneficiary" ? extractionResult.beneficiaryIdentifiers : extractionResult.contractorIdentifiers;
    if (name && name !== "Not found") setCreateName(name);
    if (address && address !== "Not found") setCreateAddress(address);
    if (identifiers && identifiers !== "Not found") setCreateCuiRo(identifiers);
  };

  const handleCreateNew = () => {
    setMode("create");
    prefillFromScan("beneficiary");
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
        prefillSource={prefillSource}
        error={error}
        saving={saving}
        onPrefillBeneficiary={() => prefillFromScan("beneficiary")}
        onPrefillContractor={() => prefillFromScan("contractor")}
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
        Choose an existing company or create a new one for this document.
      </p>
      <p className="mt-0.5 text-xs text-muted-foreground truncate" title={draftCase.title}>
        Document: {draftCase.title || draftCase.sourceFileName}
      </p>

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
