import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export type DocumentTemplateType =
  | "CreditorNotificationBpi"
  | "ReportArt97"
  | "PreliminaryClaimsTable"
  | "CreditorsMeetingMinutes"
  | "DefinitiveClaimsTable"
  | "FinalReportArt167"
  | "CreditorNotificationHtml"
  | "MandatoryReport"
  | "Custom";

/** Incoming (received) document types — not generated, but recognized by AI. */
export type IncomingDocumentType = "CourtOpeningDecision";

export interface IncomingDocumentReferenceStatus {
  type: IncomingDocumentType;
  exists: boolean;
  uploadedOn?: string;
  fileName?: string;
}

export interface DocumentTemplateDto {
  id: string;
  name: string;
  description: string | null;
  templateType: DocumentTemplateType;
  isSystem: boolean;
  isActive: boolean;
  stage: string | null;
  category: string | null;
  hasContent: boolean;
  createdOn: string;
  lastModifiedOn: string | null;
}

export interface DocumentTemplateDetailDto extends DocumentTemplateDto {
  bodyHtml: string | null;
}

export interface CreateDocumentTemplateRequest {
  name: string;
  description?: string;
  category?: string;
  bodyHtml?: string;
}

export interface UpdateDocumentTemplateRequest {
  name: string;
  description?: string;
  category?: string;
  bodyHtml?: string;
  isActive: boolean;
}

export interface RenderTemplateRequest {
  caseId: string;
  recipientPartyId?: string;
}

export interface RenderTemplateResult {
  renderedHtml: string;
  mergeData: Record<string, string>;
}

export interface PlaceholderField {
  key: string;
  label: string;
}

export interface PlaceholderGroup {
  group: string;
  fields: PlaceholderField[];
}

export interface RenderHtmlToPdfRequest {
  html: string;
  caseId: string;
  templateName?: string;
}

export interface SaveHtmlToCaseRequest {
  html: string;
  caseId: string;
  templateName: string;
}

// ── Friendly display names for system template types ─────────────────────────

export const SYSTEM_TEMPLATE_LABELS: Record<string, string> = {
  CreditorNotificationBpi: "Notificare creditori + publicare BPI",
  ReportArt97: "Raport 40 zile (Art. 97)",
  PreliminaryClaimsTable: "Tabel preliminar de creanțe",
  CreditorsMeetingMinutes: "Proces-verbal AGC confirmare lichidator",
  DefinitiveClaimsTable: "Tabel definitiv de creanțe",
  FinalReportArt167: "Raport final (Art. 167)",
  CreditorNotificationHtml: "Notificare deschidere procedură (HTML)",
};

/** Incoming document friendly names (received from court / external parties). */
export const INCOMING_DOCUMENT_LABELS: Record<IncomingDocumentType, string> = {
  CourtOpeningDecision: "Sentință / Hotărâre deschidere procedură",
};

export const INCOMING_DOCUMENT_DESC: Record<IncomingDocumentType, string> = {
  CourtOpeningDecision:
    "Document emis de instanță care deschide procedura de insolvență. " +
    "Încarcă un exemplu PDF — sistemul îl va analiza și va recunoaște automat documentele similare " +
    "încărcate de practicieni ca notificare de deschidere a procedurii.",
};

type UiLocale = "en" | "ro" | "hu";

const INCOMING_DOCUMENT_LABELS_BY_LOCALE: Record<UiLocale, Record<IncomingDocumentType, string>> = {
  en: {
    CourtOpeningDecision: "Court opening decision",
  },
  ro: {
    CourtOpeningDecision: "Sentință / Hotărâre deschidere procedură",
  },
  hu: {
    CourtOpeningDecision: "Eljárásmegnyitó bírósági határozat",
  },
};

const INCOMING_DOCUMENT_DESC_BY_LOCALE: Record<UiLocale, Record<IncomingDocumentType, string>> = {
  en: {
    CourtOpeningDecision:
      "Court-issued document that opens the insolvency procedure. " +
      "Upload one sample PDF and AI will learn to auto-recognize and classify similar documents uploaded later.",
  },
  ro: {
    CourtOpeningDecision:
      "Document emis de instanță care deschide procedura de insolvență. " +
      "Încarcă un exemplu PDF și AI-ul va recunoaște și clasifica automat documentele similare încărcate ulterior.",
  },
  hu: {
    CourtOpeningDecision:
      "A bíróság által kibocsátott dokumentum, amely megnyitja a fizetésképtelenségi eljárást. " +
      "Töltsön fel egy mint PDF-et, és az AI később automatikusan felismeri és osztályozza a hasonló dokumentumokat.",
  },
};

export const getIncomingDocumentLabel = (type: IncomingDocumentType, locale: UiLocale): string => {
  return INCOMING_DOCUMENT_LABELS_BY_LOCALE[locale]?.[type]
    ?? INCOMING_DOCUMENT_LABELS_BY_LOCALE.en[type]
    ?? type;
};

export const getIncomingDocumentDescription = (type: IncomingDocumentType, locale: UiLocale): string => {
  return INCOMING_DOCUMENT_DESC_BY_LOCALE[locale]?.[type]
    ?? INCOMING_DOCUMENT_DESC_BY_LOCALE.en[type]
    ?? "";
};

export const SYSTEM_TEMPLATE_STAGE: Record<string, string> = {
  CreditorNotificationBpi: "Deschidere procedură",
  ReportArt97: "Observație",
  PreliminaryClaimsTable: "Verificare creanțe",
  CreditorsMeetingMinutes: "Adunarea creditorilor",
  DefinitiveClaimsTable: "Lichidare",
  FinalReportArt167: "Închidere",
  CreditorNotificationHtml: "Deschidere procedură",
};

// ── API ───────────────────────────────────────────────────────────────────────

export const documentTemplatesApi = {
  /** List all templates (system + tenant custom). */
  getAll: () =>
    client.get<DocumentTemplateDto[]>("/document-templates"),

  /** Get single template with BodyHtml. */
  getById: (id: string) =>
    client.get<DocumentTemplateDetailDto>(`/document-templates/${id}`),

  /** Get grouped placeholder list for the editor sidebar. */
  getPlaceholders: () =>
    client.get<PlaceholderGroup[]>("/document-templates/placeholders"),

  /** Create a new custom template. */
  create: (req: CreateDocumentTemplateRequest) =>
    client.post<DocumentTemplateDto>("/document-templates", req),

  /** Update an existing template (custom: all fields; system: only bodyHtml + isActive). */
  update: (id: string, req: UpdateDocumentTemplateRequest) =>
    client.put<DocumentTemplateDetailDto>(`/document-templates/${id}`, req),

  /** Delete a custom template. */
  delete: (id: string) =>
    client.delete(`/document-templates/${id}`),

  /** Render template HTML against a real case. */
  render: (id: string, req: RenderTemplateRequest) =>
    client.post<RenderTemplateResult>(`/document-templates/${id}/render`, req),

  /**
   * Render a template to PDF and return it as a Blob for direct download.
   * Uses PuppeteerSharp on the server — no file is stored.
   */
  renderPdfBlob: async (
    id: string,
    req: RenderTemplateRequest,
  ): Promise<{ blob: Blob; fileName: string }> => {
    const token = localStorage.getItem("authToken");
    const tenantId = localStorage.getItem("selectedTenantId");
    const res = await fetch(`/api/document-templates/${id}/render-pdf`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
      },
      body: JSON.stringify(req),
    });
    if (!res.ok) throw new Error(`Server returned ${res.status}`);
    const blob = await res.blob();
    const cd = res.headers.get("Content-Disposition") ?? "";
    const match = cd.match(/filename\*?=(?:UTF-8'')?([^";]+)/);
    const fileName = match ? decodeURIComponent(match[1].trim()) : `template_${id}.pdf`;
    return { blob, fileName };
  },

  /**
   * Render the template to PDF and save it as an InsolvencyDocument on the case.
   * Returns { documentId, fileName, requiresSignature }.
   */
  saveToCase: (id: string, req: RenderTemplateRequest) =>
    client.post<{ documentId: string; fileName: string; storageKey: string; requiresSignature: boolean }>(
      `/document-templates/${id}/save-to-case`,
      req,
    ),

  /** Upload a sample/reference PDF for an incoming document type (AI recognition). */
  uploadIncomingReference: (type: IncomingDocumentType, file: File, onProgress?: (pct: number) => void) => {
    const form = new FormData();
    form.append("file", file);
    return client.post<{ type: string; fileName: string; fileSize: number; uploadedOn: string; message: string }>(
      `/document-templates/incoming-reference/${type}`,
      form,
      {
        headers: { "Content-Type": "multipart/form-data" },
        onUploadProgress: onProgress
          ? (e) => { if (e.total) onProgress(Math.round((e.loaded / e.total) * 100)); }
          : undefined,
      },
    );
  },

  /** Check whether a reference PDF has been uploaded for an incoming document type. */
  getIncomingReference: (type: IncomingDocumentType) =>
    client.get<IncomingDocumentReferenceStatus>(`/document-templates/incoming-reference/${type}`),
  /**
   * Convert arbitrary HTML (already rendered + optionally signed) to a PDF download.
   * Used after the user edits the preview-modal content.
   */
  renderHtmlToPdfBlob: async (
    req: RenderHtmlToPdfRequest,
  ): Promise<{ blob: Blob; fileName: string }> => {
    const token = localStorage.getItem("authToken");
    const tenantId = localStorage.getItem("selectedTenantId");
    const res = await fetch("/api/document-templates/render-html-to-pdf", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
      },
      body: JSON.stringify({ html: req.html, templateName: req.templateName }),
    });
    if (!res.ok) throw new Error(`Server returned ${res.status}`);
    const blob = await res.blob();
    const cd = res.headers.get("Content-Disposition") ?? "";
    const match = cd.match(/filename\*?=(?:UTF-8'')?([^";]+)/);
    const fileName = match ? decodeURIComponent(match[1].trim()) : `document_${Date.now()}.pdf`;
    return { blob, fileName };
  },

  /**
   * Save arbitrary HTML (reviewed / signed in the preview modal) as an InsolvencyDocument.
   * Returns { documentId, fileName, requiresSignature }.
   */
  saveToCaseFromHtml: (req: SaveHtmlToCaseRequest) =>
    client.post<{ documentId: string; fileName: string; storageKey: string; requiresSignature: boolean }>(
      "/document-templates/save-html-to-case",
      req,
    ),};
