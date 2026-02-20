import type { InsolvencyExtractionResult } from "../services/openai";
import ExtractionFieldsView, { type FieldEditNote } from "./ExtractionFieldsView";

export type { FieldEditNote };

interface ExtractionReviewStepProps {
  extractionResult: InsolvencyExtractionResult;
  sourceFileName: string;
  currentUserName: string;
  onConfirm: () => void;
  onCancel: () => void;
  onExtractionChange?: (updated: InsolvencyExtractionResult) => void;
}

export default function ExtractionReviewStep({
  extractionResult,
  sourceFileName,
  currentUserName,
  onConfirm,
  onCancel,
  onExtractionChange,
}: ExtractionReviewStepProps) {
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

      <ExtractionFieldsView
        extractionResult={extractionResult}
        currentUserName={currentUserName}
        onExtractionChange={onExtractionChange}
        showRawJson={true}
      />

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
