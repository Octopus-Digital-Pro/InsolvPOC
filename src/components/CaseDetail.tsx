import {useState} from "react";
import type {Company, InsolvencyDocument} from "../types";
import type {CaseWithDocuments} from "../hooks/useCases";
import {
  deriveCaseStage,
  aggregateDeadlines,
  formatStage,
  dateToIso,
} from "../domain/insolvencyCase";
import BackButton from "@/components/ui/BackButton";
import AssigneeDropdown from "@/components/molecules/AssigneeDropdown";
import Section from "@/components/molecules/Section";
import RawExtractionBlock from "@/components/molecules/RawExtractionBlock";
import ConfirmDeleteBar from "@/components/molecules/ConfirmDeleteBar";
import DocumentCard from "./DocumentCard";
import ExtractionFieldsView from "./ExtractionFieldsView";
import type {InsolvencyExtractionResult} from "../services/openai";
import {formatDateTime, toTitleCase} from "@/lib/dateUtils";

function isInsolvencyExtractionResult(
  raw: unknown,
): raw is InsolvencyExtractionResult {
  return (
    raw !== null &&
    typeof raw === "object" &&
    "document" in (raw as object) &&
    "case" in (raw as object) &&
    "parties" in (raw as object)
  );
}

interface CaseDetailProps {
  caseWithDocs: CaseWithDocuments;
  company?: Company | null;
  companies?: Company[];
  currentUserName: string;
  onUpdate: (
    id: string,
    updates: Partial<import("../types").InsolvencyCase>,
  ) => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onUpdateDocument?: (
    caseId: string,
    documentId: string,
    updates: Partial<InsolvencyDocument>,
  ) => Promise<void>;
  onDelete: (id: string) => void;
  onBack: () => void;
}

export default function CaseDetail({
  caseWithDocs,
  company,
  companies = [],
  currentUserName,
  onUpdate,
  onUpdateDocument,
  onDelete,
  onBack,
}: CaseDetailProps) {
  const {case: insolvencyCase, documents} = caseWithDocs;
  const [selectedDocId, setSelectedDocId] = useState<string | null>(null);
  const [showConfirmDelete, setShowConfirmDelete] = useState(false);

  const stage = deriveCaseStage(documents);
  const deadlines = aggregateDeadlines(documents);
  const selectedDoc = selectedDocId
    ? documents.find((d) => d.id === selectedDocId)
    : null;
  const nextHearing =
    documents.length > 0
      ? (documents[0].rawExtraction as InsolvencyExtractionResult | undefined)
          ?.case?.importantDates?.nextHearingDateTime
      : undefined;
  const nextHearingIso = dateToIso(nextHearing);

  return (
    <div className="mx-auto max-w-3xl pb-12">
      <BackButton onClick={onBack}>Back</BackButton>

      <div className="mb-2">
        <h1 className="text-2xl font-bold text-foreground">
          {insolvencyCase.caseNumber || "No case number"} –{" "}
          {insolvencyCase.debtorName || "Unknown debtor"}
        </h1>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
          <span>Court: {insolvencyCase.courtName || "—"}</span>
          <span className="text-border">|</span>
          <span>Stage: {formatStage(stage)}</span>
          {nextHearingIso && (
            <>
              <span className="text-border">|</span>
              <span>Next hearing: {formatDateTime(nextHearingIso)}</span>
            </>
          )}
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {company ? (
            <p className="text-sm text-muted-foreground">
              Company:{" "}
              <span className="font-medium text-foreground">
                {company.name}
              </span>
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
        <div className="my-6 flex flex-row flex-wrap gap-x-6 gap-y-2 justify-between items-center">
          {companies.length > 0 && (
            <div className="flex items-center gap-2">
              <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Company
              </label>
              <select
                value={insolvencyCase.companyId ?? ""}
                onChange={(e) =>
                  onUpdate(insolvencyCase.id, {
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
          <AssigneeDropdown dueDateDisplay={nextHearingIso ?? undefined} />
        </div>
      </div>

      <Section title="Deadlines">
        {deadlines.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No deadlines extracted yet.
          </p>
        ) : (
          <ul className="space-y-2 text-sm">
            {deadlines.slice(0, 10).map((d, i) => (
              <li key={i} className="flex flex-wrap gap-x-2 gap-y-1">
                <span className="font-medium">
                  {toTitleCase(d.type.replace(/_/g, " "))}:
                </span>
                <span className="text-muted-foreground">
                  {d.date?.iso ?? d.date?.text ?? "—"}{" "}
                  {d.time ? ` ${d.time}` : ""}
                </span>
                {d.notes && (
                  <span className="text-muted-foreground">({d.notes})</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </Section>
      <Section title="Documents">
        {documents.length === 0 ? (
          <p className="text-sm text-muted-foreground">No documents yet.</p>
        ) : (
          <div className="space-y-2">
            {documents.map((doc) => (
              <DocumentCard
                key={doc.id}
                document={doc}
                isActive={selectedDocId === doc.id}
                onClick={() =>
                  setSelectedDocId(selectedDocId === doc.id ? null : doc.id)
                }
              />
            ))}
          </div>
        )}
      </Section>

      {selectedDoc && (
        <>
          <Section title="Document detail">
            <DocumentDetailView document={selectedDoc} />
          </Section>
          {selectedDoc.rawExtraction &&
            isInsolvencyExtractionResult(selectedDoc.rawExtraction) &&
            onUpdateDocument && (
              <Section title="Extracted data">
                <ExtractionFieldsView
                  key={selectedDoc.id}
                  extractionResult={selectedDoc.rawExtraction}
                  currentUserName={currentUserName}
                  onExtractionChange={(updated) =>
                    onUpdateDocument(insolvencyCase.id, selectedDoc.id, {
                      rawExtraction: updated,
                    })
                  }
                  showRawJson={false}
                />
              </Section>
            )}
        </>
      )}

      <ConfirmDeleteBar
        isDraft={false}
        showConfirm={showConfirmDelete}
        onConfirmDelete={() => onDelete(insolvencyCase.id)}
        onCancelConfirm={() => setShowConfirmDelete(false)}
        onStartConfirm={() => setShowConfirmDelete(true)}
      />
    </div>
  );
}

function DocumentDetailView({document}: {document: InsolvencyDocument}) {
  const raw = document.rawExtraction as InsolvencyExtractionResult | undefined;
  const rawJson = raw?.rawJson ?? "{}";
  const docType = document.docType?.replace(/_/g, " ") ?? "—";
  return (
    <div className="space-y-4">
      <div className="text-sm text-muted-foreground">
        <p>
          <span className="font-medium">Type:</span> {docType}
        </p>
        <p>
          <span className="font-medium">Document date:</span>{" "}
          {document.documentDate}
        </p>
        <p>
          <span className="font-medium">File:</span> {document.sourceFileName}
        </p>
        <p>
          <span className="font-medium">Uploaded:</span>{" "}
          {formatDateTime(document.uploadedAt)} by {document.uploadedBy}
        </p>
      </div>
      {raw?.parties?.debtor && (
        <div className="text-sm">
          <p className="font-medium text-foreground">Debtor</p>
          <p className="text-muted-foreground">
            {raw.parties.debtor.name} · CUI: {raw.parties.debtor.cui}
          </p>
        </div>
      )}
      {raw?.parties?.practitioner?.name && (
        <div className="text-sm">
          <p className="font-medium text-foreground">Practitioner</p>
          <p className="text-muted-foreground">
            {raw.parties.practitioner.name}
          </p>
        </div>
      )}
      <RawExtractionBlock rawJson={rawJson} />
    </div>
  );
}
