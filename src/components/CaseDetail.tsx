import {useState} from "react";
import type { Company, ContractCase, EditHistoryEntry } from "../types";

import BackButton from "@/components/ui/BackButton";
import AssigneeDropdown from "@/components/molecules/AssigneeDropdown";
import EditableField from "@/components/molecules/EditableField";
import Section from "@/components/molecules/Section";
import EditHistoryList from "@/components/molecules/EditHistoryList";
import RawExtractionBlock from "@/components/molecules/RawExtractionBlock";
import ConfirmDeleteBar from "@/components/molecules/ConfirmDeleteBar";

interface CaseDetailProps {
  contractCase: ContractCase;
  company?: Company | null;
  companies?: Company[];
  currentUserName: string;
  onUpdate: (id: string, updates: Partial<ContractCase>) => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onDelete: (id: string) => void;
  onBack: () => void;
  /** When true, case is not saved to DB yet; show Save button and Delete discards draft */
  isDraft?: boolean;
  /** Called when user clicks Save (only when isDraft) */
  onSave?: () => void;
}

/** Human-readable labels for editable case fields (for edit history). */
const FIELD_LABELS: Record<string, string> = {
  beneficiary: "Beneficiary (Contracting Authority)",
  beneficiaryAddress: "Beneficiary Address",
  beneficiaryIdentifiers: "Beneficiary Identifiers (VAT/CUI)",
  contractor: "Contractor",
  contractorAddress: "Contractor Address",
  contractorIdentifiers: "Contractor Identifiers (VAT/CUI)",
  subcontractors: "Subcontractors",
  contractTitleOrSubject: "Title / Subject",
  contractNumberOrReference: "Contract No. / Reference",
  procurementProcedure: "Procurement Procedure",
  cpvCodes: "CPV Codes",
  contractDate: "Contract Date",
  effectiveDate: "Effective Date",
  contractPeriod: "Contract Period",
  signatories: "Signatories",
  signingLocation: "Signing Location",
  otherImportantClauses: "Other Important Clauses",
};

export default function CaseDetail({
  contractCase,
  company,
  companies = [],
  currentUserName,
  onUpdate,
  onDelete,
  onBack,
  isDraft = false,
  onSave,
}: CaseDetailProps) {
  const createdDate = new Date(contractCase.createdAt);
  const formattedDate = createdDate.toLocaleDateString("en-GB", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  });
  const formattedTime = createdDate.toLocaleTimeString("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
  });

  const handleFieldSave = (key: string, value: string) => {
    const existingEdits = contractCase.edits ?? {};
    const now = new Date().toISOString();
    const historyEntry: EditHistoryEntry = {
      at: now,
      by: currentUserName,
      field: FIELD_LABELS[key] ?? key,
      oldValue: (contractCase as unknown as Record<string, unknown>)[key] as
        | string
        | undefined,
      newValue: value || undefined,
    };
    const existingHistory = contractCase.editHistory ?? [];
    const updates: Partial<ContractCase> = {
      [key]: value,
      edits: {
        ...existingEdits,
        [key]: {editedBy: currentUserName, editedAt: now},
      },
      editHistory: [historyEntry, ...existingHistory],
    };
    if (key === "contractTitleOrSubject") {
      updates.title = value;
    }
    onUpdate(contractCase.id, updates);
  };

  const [showConfirmDelete, setShowConfirmDelete] = useState(false);

  const edits = contractCase.edits ?? {};

  const F = (label: string, key: keyof ContractCase, multiline?: boolean) => (
    <EditableField
      label={label}
      value={contractCase[key] as string}
      fieldKey={key}
      multiline={multiline}
      editInfo={edits[key]}
      onSave={handleFieldSave}
    />
  );

  return (
    <div className="mx-auto max-w-3xl pb-12">
      <BackButton onClick={onBack}>Back</BackButton>

      {/* Header */}
      <div className="mb-2">
        <h1 className="text-2xl font-bold text-foreground">
          {contractCase.title && contractCase.beneficiary
            ? `${contractCase.title} - ${contractCase.beneficiary}`
            : (contractCase.title || contractCase.beneficiary || "Untitled")}
        </h1>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
          <span>
            Created {formattedDate} at {formattedTime}
          </span>
          {contractCase.createdBy && (
            <>
              <span className="text-border">|</span>
              <span>by {contractCase.createdBy}</span>
            </>
          )}
          <span className="text-border">|</span>
          <span className="font-mono">{contractCase.sourceFileName}</span>
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {company ? (
            <p className="text-sm text-muted-foreground">
              Company:{" "}
              <span className="font-medium text-foreground">{company.name}</span>
              {company.cuiRo && (
                <span className="ml-2 text-muted-foreground">
                  CUI/RO: {company.cuiRo}
                </span>
              )}
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">Company: —</p>
          )}
        </div>
        <div className="my-8 flex flex-row flex-wrap gap-x-6 gap-y-2 justify-between items-center">
          {companies.length > 0 && (
            <div className="flex items-center gap-2">
              <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Company
              </label>
              <select
                value={contractCase.companyId ?? ""}
                onChange={(e) =>
                  onUpdate(contractCase.id, {
                    companyId: e.target.value || undefined,
                  })
                }
                className="rounded-md border border-input bg-background px-2 py-1.5 text-xs text-foreground min-w-32"
              >
                <option value="">No company</option>
                {companies.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <AssigneeDropdown dueDateDisplay={contractCase.contractDate} />
        </div>
      </div>

      <Section title="Parties">
        {F("Beneficiary (Contracting Authority)", "beneficiary")}
        {F("Beneficiary Address", "beneficiaryAddress")}
        {F("Beneficiary Identifiers (VAT/CUI)", "beneficiaryIdentifiers")}
        {F("Contractor", "contractor")}
        {F("Contractor Address", "contractorAddress")}
        {F("Contractor Identifiers (VAT/CUI)", "contractorIdentifiers")}
        {F("Subcontractors", "subcontractors", true)}
      </Section>

      <Section title="Contract Identity">
        {F("Title / Subject", "contractTitleOrSubject")}
        {F("Contract No. / Reference", "contractNumberOrReference")}
        {F("Procurement Procedure", "procurementProcedure")}
        {F("CPV Codes", "cpvCodes")}
      </Section>

      <Section title="Dates & Period">
        {F("Contract Date", "contractDate")}
        {F("Effective Date", "effectiveDate")}
        {F("Contract Period", "contractPeriod")}
      </Section>

      <Section title="Signatures">
        {F("Signatories", "signatories", true)}
        {F("Signing Location", "signingLocation")}
      </Section>

      <Section title="Other">
        {F("Other Important Clauses", "otherImportantClauses", true)}
      </Section>

      <RawExtractionBlock rawJson={contractCase.rawJson} />

      <EditHistoryList editHistory={contractCase.editHistory} />

      <ConfirmDeleteBar
        isDraft={!!isDraft}
        showConfirm={showConfirmDelete}
        onConfirmDelete={() => onDelete(contractCase.id)}
        onCancelConfirm={() => setShowConfirmDelete(false)}
        onStartConfirm={() => setShowConfirmDelete(true)}
        onSave={isDraft ? onSave : undefined}
      />
    </div>
  );
}
