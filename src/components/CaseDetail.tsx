import {useState, useRef, useEffect} from "react";
import type {ContractCase, FieldEdit} from "../types";
import {USERS, type User} from "../types";

interface CaseDetailProps {
  contractCase: ContractCase;
  currentUserName: string;
  onUpdate: (id: string, updates: Partial<ContractCase>) => void;
  onDelete: (id: string) => void;
  onBack: () => void;
}

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
  assignedTo,
  onSelect,
  dueDateDisplay,
}: {
  assignedTo: string | undefined;
  onSelect: (userId: string | null) => void;
  dueDateDisplay?: string;
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const selectedUser: User | null = assignedTo
    ? (USERS.find((u) => u.id === assignedTo) ?? null)
    : null;

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
        {/* Assigner */}
        <div className="flex flex-row align-center items-center gap-x-2">
          <label className="text-xs  font-semibold uppercase tracking-wide text-gray-400">
            Assigned to
          </label>
          <div className="relative">
            <button
              type="button"
              onClick={() => setOpen((o) => !o)}
              className="flex items-center gap-2 rounded-lg border cursor-pointer border-gray-200 bg-white px-2.5 py-1.5 text-left text-sm transition-colors hover:border-gray-300 hover:bg-gray-50 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              aria-expanded={open}
              aria-haspopup="listbox"
            >
              {selectedUser ? (
                <>
                  <img
                    src={selectedUser.avatar}
                    alt=""
                    className="h-6 w-6 shrink-0 rounded-full object-cover"
                  />
                  <span className="font-medium text-gray-800">
                    {selectedUser.name}
                  </span>
                </>
              ) : (
                <span className="text-gray-400">— Unassigned —</span>
              )}
              <svg
                className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${open ? "rotate-180" : ""}`}
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M19 9l-7 7-7-7"
                />
              </svg>
            </button>
            {open && (
              <div
                className="absolute left-0 top-full z-10 mt-1 w-max max-w-48 rounded-xl border border-gray-200 bg-white py-1 shadow-lg"
                role="listbox"
              >
                <button
                  type="button"
                  role="option"
                  onClick={() => {
                    onSelect(null);
                    setOpen(false);
                  }}
                  className={`flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-gray-50 ${
                    !selectedUser ? "bg-blue-50 text-blue-800" : "text-gray-700"
                  }`}
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gray-100 text-xs text-gray-400">
                    👨🏻‍💼
                  </span>
                  <span className={!selectedUser ? "font-medium" : ""}>
                    Unassigned
                  </span>
                </button>
                {USERS.map((u) => (
                  <button
                    key={u.id}
                    type="button"
                    role="option"
                    onClick={() => {
                      onSelect(u.id);
                      setOpen(false);
                    }}
                    className={`flex w-full items-center cursor-pointer gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-gray-50 ${
                      selectedUser?.id === u.id
                        ? "bg-blue-50 text-blue-800"
                        : "text-gray-700"
                    }`}
                  >
                    <img
                      src={u.avatar}
                      alt=""
                      className="h-7 w-7 shrink-0 rounded-full object-cover"
                    />
                    <div>
                      <p
                        className={`font-medium ${selectedUser?.id === u.id ? "text-blue-800" : "text-gray-800"}`}
                      >
                        {u.name}
                      </p>
                      <p className="text-xs text-gray-400">{u.role}</p>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
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
      {/* Set Alert Button */}
      <div className="flex items-center gap-2">
        <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
          Set Alert
        </label>
        <button className="rounded-md bg-blue-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-600">
          Set Alert
        </button>
      </div>
    </div>
  );
}

export default function CaseDetail({
  contractCase,
  currentUserName,
  onUpdate,
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
    const updates: Partial<ContractCase> = {
      [key]: value,
      edits: {
        ...existingEdits,
        [key]: {editedBy: currentUserName, editedAt: new Date().toISOString()},
      },
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
        className="mb-4 flex items-center gap-1.5 text-sm text-gray-400 hover:text-blue-500 transition-colors"
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
        Upload new document
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
        <AssigneeDropdown
          assignedTo={contractCase.assignedTo}
          onSelect={(userId) =>
            onUpdate(contractCase.id, {assignedTo: userId ?? undefined})
          }
          dueDateDisplay={contractCase.contractDate}
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
