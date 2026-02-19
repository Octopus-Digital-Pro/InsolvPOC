import {useState, useRef, useEffect} from "react";
import {format} from "date-fns";
import type {
  Company,
  ContractCase,
  EditHistoryEntry,
  FieldEdit,
} from "../types";
import {DatePicker} from "@/components/ui/date-picker";

interface CaseDetailProps {
  contractCase: ContractCase;
  company?: Company | null;
  companies?: Company[];
  currentUserName: string;
  onUpdate: (id: string, updates: Partial<ContractCase>) => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onDelete: (id: string) => void;
  onBack: () => void;
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

function formatEditDate(iso: string): string {
  const d = new Date(iso);
  return (
    d.toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric",
    }) +
    " at " +
    d.toLocaleTimeString("en-GB", {hour: "2-digit", minute: "2-digit"})
  );
}

function EditableField({
  label,
  value,
  fieldKey,
  multiline,
  editInfo,
  onSave,
}: {
  label: string;
  value: string;
  fieldKey: string;
  multiline?: boolean;
  editInfo?: FieldEdit;
  onSave: (key: string, value: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  const handleSave = () => {
    onSave(fieldKey, draft);
    setEditing(false);
  };

  const handleCancel = () => {
    setDraft(value);
    setEditing(false);
  };

  return (
    <div className="group rounded-lg border border-gray-100 bg-white p-4 transition-colors hover:border-gray-200">
      <div className="mb-1.5 flex items-center justify-between">
        <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
          {label}
        </label>
        {!editing && (
          <button
            onClick={() => setEditing(true)}
            className="text-xs text-gray-400 opacity-0 transition-opacity cursor-pointer group-hover:opacity-100 hover:text-blue-500"
          >
            Edit
          </button>
        )}
      </div>

      {editing ? (
        <div>
          {multiline ? (
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              rows={4}
              className="w-full rounded-md border border-gray-200 p-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              autoFocus
            />
          ) : (
            <input
              type="text"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full rounded-md border border-gray-200 p-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              autoFocus
            />
          )}
          <div className="mt-2 flex gap-2">
            <button
              onClick={handleSave}
              className="rounded-md bg-blue-500 px-3 py-1 text-xs font-medium text-white hover:bg-blue-600"
            >
              Save
            </button>
            <button
              onClick={handleCancel}
              className="rounded-md bg-gray-100 px-3 py-1 text-xs font-medium text-gray-600 hover:bg-gray-200"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <>
          <p className="whitespace-pre-wrap text-sm text-gray-700">
            {value || <span className="italic text-gray-300">Empty</span>}
          </p>
          {editInfo && (
            <p className="mt-1.5 text-[11px] italic  text-blue-800">
              Edited by {editInfo.editedBy} on{" "}
              {formatEditDate(editInfo.editedAt)}
            </p>
          )}
        </>
      )}
    </div>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="mt-6">
      <h3 className="mb-3 text-xs font-bold uppercase tracking-wider text-gray-500 border-b border-gray-100 pb-2">
        {title}
      </h3>
      <div className="space-y-2">{children}</div>
    </div>
  );
}

function AssigneeDropdown({
  dueDateDisplay,
  alertAt,
  onSetAlert,
}: {
  assignedTo: string | undefined;
  onSelect: (userId: string | null) => void;
  dueDateDisplay?: string;
  alertAt?: string;
  onSetAlert?: (iso: string | undefined) => void;
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  return (
    <div
      className="my-8 flex flex-row  gap-x-6 gap-y-2 justify-between"
      ref={containerRef}
    >
      <div className="flex flex-row items-center gap-6">
        {/* Due Date */}
        <div className="flex items-center gap-2">
          <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
            Due date
          </label>
          <span className="text-sm text-gray-800">
            {dueDateDisplay && dueDateDisplay !== "Not found"
              ? dueDateDisplay
              : "—"}
          </span>
        </div>
      </div>
      {/* Alert (date only) */}
      <div className="flex items-center gap-2">
        <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
          Notification
        </label>
        {alertAt ? (
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-800">
              {formatAlertDate(alertAt)}
            </span>
            <button
              type="button"
              onClick={() => onSetAlert?.(undefined)}
              className="text-xs text-gray-400 hover:text-red-600 cursor-pointer underline"
            >
              Clear
            </button>
          </div>
        ) : (
          <DatePicker
            date={undefined}
            onSelect={(d) => {
              if (d) onSetAlert?.(format(d, "yyyy-MM-dd"));
            }}
            placeholder="Pick a date"
            className="min-w-40"
          />
        )}
      </div>
    </div>
  );
}

/** Format a date-only string (YYYY-MM-DD) or ISO string for display as DD.MM.YYYY. */
function formatAlertDate(dateString: string): string {
  const d = new Date(
    dateString.includes("T") ? dateString : `${dateString}T12:00:00`,
  );
  return format(d, "dd.MM.yyyy");
}

export default function CaseDetail({
  contractCase,
  company,
  companies = [],
  currentUserName,
  onUpdate,
  onUpdateCompany,
  onDelete,
  onBack,
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
      {/* Back / New upload */}
      <button
        onClick={onBack}
        className="mb-4 flex items-center cursor-pointer gap-1.5 text-sm text-gray-400 hover:text-blue-500 transition-colors"
      >
        <svg
          className="h-4 w-4"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M15 19l-7-7 7-7"
          />
        </svg>
        Back
      </button>

      {/* Header */}
      <div className="mb-2">
        <h1 className="text-2xl font-bold text-gray-900">
          {contractCase.title + " - " + contractCase.beneficiary || "Untitled"}
        </h1>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-400">
          <span>
            Created {formattedDate} at {formattedTime}
          </span>
          {contractCase.createdBy && (
            <>
              <span className="text-gray-300">|</span>
              <span>by {contractCase.createdBy}</span>
            </>
          )}
          <span className="text-gray-300">|</span>
          <span className="font-mono">{contractCase.sourceFileName}</span>
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {company ? (
            <p className="text-sm text-gray-600">
              Company:{" "}
              <span className="font-medium text-gray-800">{company.name}</span>
              {company.cuiRo && (
                <span className="ml-2 text-gray-500">
                  CUI/RO: {company.cuiRo}
                </span>
              )}
            </p>
          ) : (
            <p className="text-sm text-gray-500">Company: —</p>
          )}
          {companies.length > 0 && (
            <select
              value={contractCase.companyId ?? ""}
              onChange={(e) =>
                onUpdate(contractCase.id, {
                  companyId: e.target.value || undefined,
                })
              }
              className="rounded-md border border-gray-200 px-2 py-1 text-xs text-gray-700"
            >
              <option value="">No company</option>
              {companies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          )}
        </div>
        <AssigneeDropdown
          assignedTo={company?.assignedTo}
          onSelect={
            company && onUpdateCompany
              ? (userId) =>
                  onUpdateCompany(company.id, {assignedTo: userId ?? undefined})
              : () => {}
          }
          dueDateDisplay={contractCase.contractDate}
          alertAt={contractCase.alertAt}
          onSetAlert={(iso) =>
            onUpdate(contractCase.id, {alertAt: iso ?? undefined})
          }
        />
      </div>

      {/* --- Grouped fields --- */}

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

      {/* Raw extraction (collapsible) */}
      <details className="mt-6 rounded-lg border border-gray-100 bg-gray-50">
        <summary className="cursor-pointer px-4 py-3 text-xs font-medium text-gray-400 hover:text-gray-600">
          Raw AI Extraction (JSON)
        </summary>
        <pre className="overflow-x-auto whitespace-pre-wrap px-4 pb-4 font-mono text-xs text-gray-500">
          {contractCase.rawJson}
        </pre>
      </details>

      {/* Edit history (Asana/Jira-style activity) */}
      <details
        className="mt-4 rounded-lg border border-gray-100 bg-gray-50"
        open={false}
      >
        <summary className="cursor-pointer px-4 py-3 text-xs font-medium text-gray-400 hover:text-gray-600">
          Edit history
          {(contractCase.editHistory?.length ?? 0) > 0 && (
            <span className="ml-2 text-gray-400">
              ({contractCase.editHistory?.length ?? 0})
            </span>
          )}
        </summary>
        <div className="border-t border-gray-100 px-4 pb-4 pt-2">
          {!contractCase.editHistory?.length ? (
            <p className="text-xs text-gray-400 italic">No edits yet.</p>
          ) : (
            <ul className="space-y-3">
              {contractCase.editHistory.map((entry, i) => (
                <li
                  key={`${entry.at}-${entry.field}-${i}`}
                  className="flex gap-3 text-sm"
                >
                  <span className="shrink-0 text-xs text-gray-400 tabular-nums">
                    {formatEditDate(entry.at)}
                  </span>
                  <span className="text-gray-600">
                    <span className="font-medium text-gray-800">
                      {entry.by}
                    </span>
                    {" changed "}
                    <span className="font-medium text-gray-700">
                      {entry.field}
                    </span>
                    {entry.oldValue !== undefined &&
                    entry.newValue !== undefined ? (
                      <>
                        {" from "}
                        <span
                          className="text-gray-500 line-clamp-1 max-w-48 align-middle"
                          title={entry.oldValue}
                        >
                          {entry.oldValue || "—"}
                        </span>
                        {" to "}
                        <span
                          className="text-gray-700 line-clamp-1 max-w-48 align-middle"
                          title={entry.newValue}
                        >
                          {entry.newValue || "—"}
                        </span>
                      </>
                    ) : entry.newValue ? (
                      <>
                        {" "}
                        to{" "}
                        {entry.newValue.length > 80
                          ? entry.newValue.slice(0, 80) + "…"
                          : entry.newValue}
                      </>
                    ) : (
                      " (cleared)"
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </details>

      {/* Delete */}
      <div className="mt-8 border-t border-gray-100 pt-6">
        {showConfirmDelete ? (
          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-600">
              Delete this case permanently?
            </span>
            <button
              onClick={() => onDelete(contractCase.id)}
              className="rounded-md bg-red-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-600"
            >
              Yes, delete
            </button>
            <button
              onClick={() => setShowConfirmDelete(false)}
              className="rounded-md bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-200"
            >
              Cancel
            </button>
          </div>
        ) : (
          <button
            onClick={() => setShowConfirmDelete(true)}
            className="text-xs text-black-200 hover:cursor-pointer hover:text-red-500 rounded-md border border-black-200 px-3 py-1.5  font-medium  transition-colors hover:border-black-500 hover:bg-gray-50"
          >
            Delete case
          </button>
        )}
      </div>
    </div>
  );
}
//rounded-md border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-500 transition-colors hover:border-gray-300 hover:bg-gray-50 hover:text-gray-700
