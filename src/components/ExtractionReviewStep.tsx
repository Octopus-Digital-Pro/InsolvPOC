import { useState } from "react";
import type { InsolvencyExtractionResult, InsolvencyDate } from "../services/openai";
import Section from "./molecules/Section";
import RawExtractionBlock from "./molecules/RawExtractionBlock";
import { formatEditDate } from "@/lib/dateUtils";

export interface FieldEditNote {
  editedBy: string;
  editedAt: string;
  from: string;
  to: string;
}

interface ExtractionReviewStepProps {
  extractionResult: InsolvencyExtractionResult;
  sourceFileName: string;
  currentUserName: string;
  onConfirm: () => void;
  onCancel: () => void;
  onExtractionChange?: (updated: InsolvencyExtractionResult) => void;
}

function dateStr(d?: InsolvencyDate): string {
  if (!d) return "—";
  return d.iso ?? d.text ?? "—";
}

function getByPath(obj: unknown, path: string): string {
  const parts = path.split(".");
  let cur: unknown = obj;
  for (const p of parts) {
    if (cur == null || typeof cur !== "object") return "—";
    cur = (cur as Record<string, unknown>)[p];
  }
  if (cur == null) return "—";
  if (typeof cur === "object" && "iso" in (cur as object) && "text" in (cur as object)) {
    const d = cur as InsolvencyDate;
    return d.iso ?? d.text ?? "—";
  }
  if (Array.isArray(cur)) return cur.map(String).join(", ");
  return String(cur);
}

function setByPath(
  obj: InsolvencyExtractionResult,
  path: string,
  value: string,
  isDatePath: boolean,
): InsolvencyExtractionResult {
  const parts = path.split(".");
  const result = JSON.parse(JSON.stringify(obj)) as InsolvencyExtractionResult;
  let cur: Record<string, unknown> = result as unknown as Record<string, unknown>;
  for (let i = 0; i < parts.length - 1; i++) {
    const p = parts[i];
    if (cur[p] == null) cur[p] = {};
    cur = cur[p] as Record<string, unknown>;
  }
  const last = parts[parts.length - 1];
  if (isDatePath) {
    cur[last] = { text: value, iso: value };
  } else if (path.endsWith("administrationRightLifted")) {
    cur[last] = value === "true" ? true : value === "false" ? false : null;
  } else if (path.endsWith("legalBasisArticles")) {
    cur[last] = value ? value.split(",").map((s) => s.trim()).filter(Boolean) : [];
  } else if (path.endsWith("shareCapitalRon")) {
    cur[last] = value === "" || value === "—" ? null : Number(value);
  } else {
    cur[last] = value;
  }
  return result;
}

const DATE_PATHS = new Set([
  "document.documentDate",
  "case.importantDates.requestFiledDate",
  "case.importantDates.openingDate",
  "case.importantDates.nextHearingDateTime",
  "parties.practitioner.appointedDate",
  "parties.practitioner.confirmedDate",
]);

function EditableField({
  label,
  value,
  fieldPath,
  multiline,
  editInfo,
  onSave,
}: {
  label: string;
  value: string;
  fieldPath: string;
  multiline?: boolean;
  editInfo?: FieldEditNote;
  onSave: (value: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  const handleSave = () => {
    onSave(draft);
    setEditing(false);
  };

  const handleCancel = () => {
    setDraft(value);
    setEditing(false);
  };

  return (
    <div
      className="group rounded-lg border border-border bg-card p-4 transition-colors hover:border-border/80"
      data-field={fieldPath}
    >
      <div className="mb-1.5 flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {label}
        </span>
        {!editing && (
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="text-xs text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100 hover:text-primary"
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
              className="w-full rounded-md border border-input bg-background p-2 text-sm text-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
              autoFocus
            />
          ) : (
            <input
              type="text"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full rounded-md border border-input bg-background p-2 text-sm text-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
              autoFocus
            />
          )}
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              onClick={handleSave}
              className="rounded-md bg-primary px-3 py-1 text-xs font-medium text-primary-foreground hover:bg-primary/90"
            >
              Save
            </button>
            <button
              type="button"
              onClick={handleCancel}
              className="rounded-md bg-muted px-3 py-1 text-xs font-medium text-muted-foreground hover:bg-muted/80"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <>
          <p className="whitespace-pre-wrap text-sm text-foreground">
            {value || <span className="italic text-muted-foreground/60">Empty</span>}
          </p>
          {editInfo && (
            <p className="mt-1.5 text-[11px] italic text-amber-600 dark:text-amber-400">
              Edited by {editInfo.editedBy} on {formatEditDate(editInfo.editedAt)} · {editInfo.from || "—"} → {editInfo.to || "—"}
            </p>
          )}
        </>
      )}
    </div>
  );
}

export default function ExtractionReviewStep({
  extractionResult,
  sourceFileName,
  currentUserName,
  onConfirm,
  onCancel,
  onExtractionChange,
}: ExtractionReviewStepProps) {
  const [draft, setDraft] = useState<InsolvencyExtractionResult>(() =>
    JSON.parse(JSON.stringify(extractionResult)),
  );
  const [edits, setEdits] = useState<Record<string, FieldEditNote>>({});

  const caseData = draft.case;
  const deadlines = draft.deadlines ?? [];
  const claims = draft.claims;
  const complianceFlags = draft.complianceFlags;

  const handleFieldSave = (path: string, newValue: string) => {
    const oldValue = getByPath(draft, path);
    const isDatePath = DATE_PATHS.has(path);
    const next = setByPath(draft, path, newValue, isDatePath);
    setDraft(next);
    setEdits((prev) => ({
      ...prev,
      [path]: {
        editedBy: currentUserName,
        editedAt: new Date().toISOString(),
        from: oldValue,
        to: newValue,
      },
    }));
    onExtractionChange?.(next);
  };

  const F = (path: string, label: string, multiline?: boolean) => (
    <EditableField
      label={label}
      value={getByPath(draft, path)}
      fieldPath={path}
      multiline={multiline}
      editInfo={edits[path]}
      onSave={(value) => handleFieldSave(path, value)}
    />
  );

  const deadlinesSummary =
    deadlines.length === 0
      ? "None extracted."
      : deadlines
          .map(
            (d) =>
              `${d.type?.replace(/_/g, " ") ?? "—"} – ${dateStr(d.date)}${
                d.time ? ` ${d.time}` : ""
              }${d.notes ? ` – ${d.notes}` : ""}`,
          )
          .join("\n");

  const claimsSummary = claims
    ? [
        `Table type: ${claims.tableType ?? "—"}`,
        `Table date: ${dateStr(claims.tableDate)}`,
        `Total declared (RON): ${
          claims.totalDeclaredRon != null ? String(claims.totalDeclaredRon) : "—"
        }`,
        `Total admitted (RON): ${
          claims.totalAdmittedRon != null ? String(claims.totalAdmittedRon) : "—"
        }`,
        `Currency: ${claims.currency ?? "—"}`,
      ].join("\n")
    : "No claims extracted.";

  const complianceSummary = complianceFlags
    ? [
        `Administration right lifted: ${
          complianceFlags.administrationRightLifted == null
            ? "—"
            : String(complianceFlags.administrationRightLifted)
        }`,
        `Individual actions suspended: ${
          complianceFlags.individualActionsSuspended == null
            ? "—"
            : String(complianceFlags.individualActionsSuspended)
        }`,
        `Publication in BPI referenced: ${
          complianceFlags.publicationInBPIReferenced == null
            ? "—"
            : String(complianceFlags.publicationInBPIReferenced)
        }`,
      ].join("\n")
    : "No compliance flags extracted.";

  return (
    <div className="mx-auto max-w-3xl pb-12">
      <h2 className="text-2xl font-bold text-foreground">Review extracted data</h2>
      <p className="mt-2 text-sm text-muted-foreground">
        Review and edit the fields below before adding this document to the system. Nothing
        is saved until you confirm and then attach it to a company.
      </p>
      <p className="mt-2 text-xs text-muted-foreground truncate" title={sourceFileName}>
        File: {sourceFileName}
      </p>

      <Section title="Document">
        {F("document.docType", "Type")}
        {F("document.language", "Language")}
        {F("document.issuingEntity", "Issuing entity")}
        {F("document.documentNumber", "Document number")}
        {F("document.documentDate", "Document date")}
        {F("document.sourceHints", "Source hints", true)}
      </Section>

      <Section title="Case & court">
        {F("case.caseNumber", "Case number")}
        {F("case.court.name", "Court name")}
        {F("case.court.section", "Section")}
        {F("case.court.registryAddress", "Registry address")}
        {F("case.court.registryPhone", "Registry phone")}
        {F("case.court.registryHours", "Registry hours")}
        {F("case.judgeSyndic", "Judge / syndic")}
        {caseData?.procedure && (
          <>
            {F("case.procedure.law", "Procedure law")}
            {F("case.procedure.procedureType", "Procedure type")}
            {F("case.procedure.stage", "Stage")}
            {F("case.procedure.administrationRightLifted", "Administration right lifted")}
            {F("case.procedure.legalBasisArticles", "Legal basis articles")}
          </>
        )}
        {caseData?.importantDates && (
          <>
            {F("case.importantDates.requestFiledDate", "Request filed date")}
            {F("case.importantDates.openingDate", "Opening date")}
            {F("case.importantDates.nextHearingDateTime", "Next hearing")}
          </>
        )}
      </Section>

      <Section title="Debtor">
        {F("parties.debtor.name", "Name")}
        {F("parties.debtor.cui", "CUI")}
        {F("parties.debtor.tradeRegisterNo", "Trade register no.")}
        {F("parties.debtor.address", "Address")}
        {F("parties.debtor.locality", "Locality")}
        {F("parties.debtor.county", "County")}
        {F("parties.debtor.administrator", "Administrator")}
        {F("parties.debtor.associateOrShareholder", "Associate / shareholder")}
        {F("parties.debtor.caen", "CAEN")}
        {F("parties.debtor.incorporationYear", "Incorporation year")}
        {F("parties.debtor.shareCapitalRon", "Share capital (RON)")}
      </Section>

      <Section title="Practitioner">
        {F("parties.practitioner.role", "Role")}
        {F("parties.practitioner.name", "Name")}
        {F("parties.practitioner.fiscalId", "Fiscal ID")}
        {F("parties.practitioner.rfo", "RFO")}
        {F("parties.practitioner.representative", "Representative")}
        {F("parties.practitioner.address", "Address")}
        {F("parties.practitioner.email", "Email")}
        {F("parties.practitioner.phone", "Phone")}
        {F("parties.practitioner.appointedDate", "Appointed date")}
        {F("parties.practitioner.confirmedDate", "Confirmed date")}
      </Section>

      <Section title="Deadlines & claims summary">
        <EditableField
          label="Deadlines"
          value={deadlinesSummary}
          fieldPath="_deadlinesSummary"
          multiline
          editInfo={edits._deadlinesSummary}
          onSave={(value) => {
            setEdits((prev) => ({
              ...prev,
              _deadlinesSummary: {
                editedBy: currentUserName,
                editedAt: new Date().toISOString(),
                from: deadlinesSummary,
                to: value,
              },
            }));
          }}
        />
        <EditableField
          label="Claims"
          value={claimsSummary}
          fieldPath="_claimsSummary"
          multiline
          editInfo={edits._claimsSummary}
          onSave={(value) => {
            setEdits((prev) => ({
              ...prev,
              _claimsSummary: {
                editedBy: currentUserName,
                editedAt: new Date().toISOString(),
                from: claimsSummary,
                to: value,
              },
            }));
          }}
        />
      </Section>

      <Section title="Compliance">
        <EditableField
          label="Compliance flags"
          value={complianceSummary}
          fieldPath="_complianceSummary"
          multiline
          editInfo={edits._complianceSummary}
          onSave={(value) => {
            setEdits((prev) => ({
              ...prev,
              _complianceSummary: {
                editedBy: currentUserName,
                editedAt: new Date().toISOString(),
                from: complianceSummary,
                to: value,
              },
            }));
          }}
        />
      </Section>

      <Section title="Other important info">
        {F("otherImportantInfo", "Notes", true)}
      </Section>

      {draft.rawJson && (
        <div className="mt-6">
          <RawExtractionBlock rawJson={draft.rawJson} />
        </div>
      )}

      <div className="mt-8 flex flex-wrap items-center gap-4 border-t border-border pt-6">
        <button
          type="button"
          onClick={onConfirm}
          className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          Confirm and continue
        </button>
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

