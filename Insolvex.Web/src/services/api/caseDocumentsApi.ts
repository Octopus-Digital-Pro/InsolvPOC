import client from "./client";

// ── Document types ─────────────────────────────────────────────────────────

export interface CaseDocumentType {
  value: string;
  label: string;
}

export const CASE_DOCUMENT_TYPES: CaseDocumentType[] = [
  { value: "CourtOpeningDecision", label: "Court Opening Decision" },
  { value: "BpiPublication",       label: "BPI Publication" },
  { value: "CreditorNotification", label: "Creditor Notification" },
  { value: "CreditorClaim",        label: "Creditor Claim" },
  { value: "AssetInventory",       label: "Asset Inventory" },
  { value: "PractitionerReport",   label: "Practitioner Report" },
  { value: "FinancialStatement",   label: "Financial Statement" },
  { value: "TaxCertificate",       label: "Tax Certificate" },
  { value: "BankStatement",        label: "Bank Statement" },
  { value: "LiquidationReport",    label: "Liquidation Report" },
  { value: "Other",                label: "Other" },
];

// ── Result types ───────────────────────────────────────────────────────────

export interface CaseDocumentUploadResult {
  documentId: string;
  fileName: string;
  storageKey: string;
  docType: string;
  fileSizeBytes: number;
  aiExtracted: boolean;
  aiSummary: string | null;
  annotationsApplied: boolean;
  aiConfidence: number | null;
  fieldsExtractedJson: string | null;
}

// ── API ────────────────────────────────────────────────────────────────────

export const caseDocumentsApi = {
  /**
   * Upload a document to a case with an explicit document type.
   * Stores the file under cases/{caseId}/{docType}/{docId}{ext},
   * runs AI field extraction with annotation context, and returns
   * the enriched document record.
   */
  upload(
    caseId: string,
    docType: string,
    file: File,
    onProgress?: (pct: number) => void,
  ): Promise<{ data: CaseDocumentUploadResult }> {
    const formData = new FormData();
    formData.append("file", file);

    return client.post<CaseDocumentUploadResult>(
      `/cases/${caseId}/documents/upload`,
      formData,
      {
        params: { docType },
        headers: { "Content-Type": "multipart/form-data" },
        onUploadProgress: onProgress
          ? (e) => {
              if (e.total) onProgress(Math.round((e.loaded * 100) / e.total));
            }
          : undefined,
      },
    );
  },

  /** Fetch the server-side list of well-known document types. */
  getDocumentTypes() {
    return client.get<CaseDocumentType[]>("/cases/document-types");
  },

  /** Ensure folder structure exists for a case (safe to call any time). */
  ensureFolders(caseId: string) {
    return client.post(`/cases/${caseId}/documents/ensure-folders`);
  },
};
