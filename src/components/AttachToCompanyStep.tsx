import { useState, useMemo } from "react";
import type { Company, ContractCase } from "../types";
import type { ContractExtractionResult } from "../services/openai";

function normalizeForMatch(s: string): string {
  return (s || "").toLowerCase().replace(/\s+/g, " ").trim();
}

function suggestCompanies(
  companies: Company[],
  name: string,
  identifiers: string
): Company[] {
  if (!name || name === "Not found") return [];
  const n = normalizeForMatch(name);
  const idLower = (identifiers || "").toLowerCase();
  return companies.filter((c) => {
    const matchName = normalizeForMatch(c.name).includes(n) || n.includes(normalizeForMatch(c.name));
    const matchCui = c.cuiRo && idLower.includes(c.cuiRo.toLowerCase());
    return matchName || matchCui;
  });
}

interface AttachToCompanyStepProps {
  draftCase: ContractCase;
  extractionResult: ContractExtractionResult;
  companies: Company[];
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
  onCreateCompany,
  onAttach,
  onCancel,
  createdBy,
}: AttachToCompanyStepProps) {
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
      <div className="mx-auto max-w-xl pt-12">
        <h2 className="text-xl font-semibold text-gray-800">Create new company</h2>
        <p className="mt-1 text-sm text-gray-500">
          Pre-filled from scan. You can switch to contractor data or edit.
        </p>
        <div className="mt-2 flex gap-2 text-xs">
          <button
            type="button"
            onClick={() => prefillFromScan("beneficiary")}
            className={prefillSource === "beneficiary" ? "text-blue-600 font-medium" : "text-gray-500 hover:text-gray-700"}
          >
            Use beneficiary
          </button>
          <span className="text-gray-300">|</span>
          <button
            type="button"
            onClick={() => prefillFromScan("contractor")}
            className={prefillSource === "contractor" ? "text-blue-600 font-medium" : "text-gray-500 hover:text-gray-700"}
          >
            Use contractor
          </button>
        </div>
        <div className="mt-6 space-y-4">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-gray-500 mb-1">Name</label>
            <input
              type="text"
              value={createName}
              onChange={(e) => setCreateName(e.target.value)}
              className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              placeholder="Company name"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-gray-500 mb-1">CUI / RO</label>
            <input
              type="text"
              value={createCuiRo}
              onChange={(e) => setCreateCuiRo(e.target.value)}
              className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              placeholder="Tax ID"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wide text-gray-500 mb-1">Address</label>
            <textarea
              value={createAddress}
              onChange={(e) => setCreateAddress(e.target.value)}
              rows={2}
              className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              placeholder="Address"
            />
          </div>
          {error && (
            <p className="text-sm text-red-600">{error}</p>
          )}
          <div className="flex gap-3">
            <button
              type="button"
              onClick={handleSaveNewCompany}
              disabled={saving || !createName.trim()}
              className="rounded-md bg-blue-500 px-4 py-2 text-sm font-medium text-white hover:bg-blue-600 disabled:opacity-50"
            >
              {saving ? "Saving…" : "Create company & attach case"}
            </button>
            <button
              type="button"
              onClick={() => setMode("select")}
              className="rounded-md border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50"
            >
              Back
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl pt-12">
      <h2 className="text-xl font-semibold text-gray-800">Attach to company</h2>
      <p className="mt-1 text-sm text-gray-500">
        Choose an existing company or create a new one for this document.
      </p>
      <p className="mt-0.5 text-xs text-gray-400 truncate" title={draftCase.title}>
        Document: {draftCase.title || draftCase.sourceFileName}
      </p>

      <div className="mt-6">
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search companies by name or CUI…"
          className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
        />
      </div>

      {suggested.length > 0 && !search.trim() && (
        <section className="mt-6">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">Suggested</h3>
          <ul className="space-y-2">
            {suggested.slice(0, 5).map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  onClick={() => handleAttachExisting(c.id)}
                  className="w-full rounded-xl border border-gray-100 bg-white px-4 py-3 text-left text-sm transition-colors hover:border-blue-200 hover:bg-blue-50"
                >
                  <span className="font-medium text-gray-800">{c.name}</span>
                  {c.cuiRo && <span className="ml-2 text-xs text-gray-500">{c.cuiRo}</span>}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mt-6">
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
          {search.trim() ? "Search results" : "All companies"}
        </h3>
        {filteredCompanies.length === 0 ? (
          <p className="py-4 text-sm text-gray-400">No companies match.</p>
        ) : (
          <ul className="space-y-2 max-h-60 overflow-y-auto">
            {filteredCompanies.map((c) => (
              <li key={c.id}>
                <button
                  type="button"
                  onClick={() => handleAttachExisting(c.id)}
                  className="w-full rounded-xl border border-gray-100 bg-white px-4 py-3 text-left text-sm transition-colors hover:border-blue-200 hover:bg-blue-50"
                >
                  <span className="font-medium text-gray-800">{c.name}</span>
                  {c.cuiRo && <span className="ml-2 text-xs text-gray-500">{c.cuiRo}</span>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="mt-6 pt-4 border-t border-gray-100">
        <button
          type="button"
          onClick={handleCreateNew}
          className="rounded-md border border-dashed border-gray-300 px-4 py-3 text-sm font-medium text-gray-600 hover:border-blue-300 hover:bg-blue-50 hover:text-blue-700"
        >
          + Create new company
        </button>
      </div>

      <div className="mt-6">
        <button
          type="button"
          onClick={onCancel}
          className="text-sm text-gray-500 hover:text-gray-700"
        >
          Cancel (discard scan)
        </button>
      </div>
    </div>
  );
}
