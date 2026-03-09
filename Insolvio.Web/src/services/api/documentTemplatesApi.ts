import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export type DocumentTemplateType =
  | "courtOpeningDecision"
  | "creditorNotificationBpi"
  | "reportArt97"
  | "preliminaryClaimsTable"
  | "creditorsMeetingMinutes"
  | "definitiveClaimsTable"
  | "finalReportArt167"
  | "creditorNotificationHtml"
  | "mandatoryReport"
  | "custom";

/** Incoming (received) document types — not generated, but recognized by AI. */
export type IncomingDocumentType =
  | "CourtOpeningDecision"
  | "BpiPublication"
  | "CreditorNotification"
  | "CreditorClaim"
  | "AssetInventory"
  | "PractitionerReport"
  | "FinancialStatement"
  | "TaxCertificate"
  | "BankStatement"
  | "LiquidationReport"
  | "Generated"
  | "Other";

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
  pastTasksFromDate?: string;
  pastTasksToDate?: string;
  futureTasksFromDate?: string;
  futureTasksToDate?: string;
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

export interface ImportWordDocumentResult {
  html: string;
  detectedPlaceholders: string[];
  fileName: string;
}

export interface IncomingAnnotationItem {
  id: string;
  field: string;
  label: string;
  /** Exact text the user selected in the document. */
  selectedText: string;
  /** Text immediately before the selection (for identification context). */
  contextBefore: string;
  /** Text immediately after the selection (for identification context). */
  contextAfter: string;
  /** True when the annotation was pre-filled by AI and not yet manually confirmed. */
  aiSuggested?: boolean;
}

export interface IncomingAnnotationsPayload {
  annotations: IncomingAnnotationItem[];
  notes?: string | null;
}

/** Full DB profile for an incoming document type, including AI summaries in 3 languages. */
export interface IncomingDocumentProfile {
  type: string;
  exists: boolean;
  originalFileName?: string;
  fileSizeBytes?: number;
  uploadedOn?: string;
  lastAnnotatedOn?: string | null;
  annotationCount?: number;
  annotationNotes?: string | null;
  aiSummaryEn?: string | null;
  aiSummaryRo?: string | null;
  aiSummaryHu?: string | null;
  aiParametersJson?: string | null;
  aiModel?: string | null;
  aiConfidence?: number | null;
  aiAnalysedOn?: string | null;
}

/** Summary item returned by GET /incoming-reference/{type}/all */
export interface IncomingDocumentProfileEntry {
  id: string;
  type: string;
  originalFileName: string;
  fileSizeBytes: number;
  uploadedOn: string;
  lastAnnotatedOn?: string | null;
  annotationCount: number;
  isFinalized: boolean;
  finalizedOn?: string | null;
  trainingStatus?: string | null;
  aiConfidence?: number | null;
  aiAnalysedOn?: string | null;
  hasAiProfile: boolean;
}

/** Full profile returned by GET /incoming-reference/profile/{id} */
export interface IncomingDocumentProfileById extends IncomingDocumentProfile {
  id: string;
  isFinalized: boolean;
  finalizedOn?: string | null;
  trainingStatus?: string | null;
}

/** Response from POST .../finalize-and-train */
export interface FinalizeAndTrainResult {
  id: string;
  isFinalized: boolean;
  finalizedOn: string;
  trainingStatus: string;
  aiConfidence?: number | null;
  aiAnalysedOn?: string | null;
  message: string;
}

/** Response from POST .../analyse */
export interface AiDocumentProfileResult {
  type: string;
  aiSummaryEn?: string | null;
  aiSummaryRo?: string | null;
  aiSummaryHu?: string | null;
  aiParametersJson?: string | null;
  aiModel?: string | null;
  aiConfidence?: number | null;
  aiAnalysedOn?: string | null;
  message: string;
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
  creditorNotificationBpi: "Notificare creditori + publicare BPI",
  reportArt97: "Raport 40 zile (Art. 97)",
  mandatoryReport: "Raport periodic obligatoriu (30 zile)",
  preliminaryClaimsTable: "Tabel preliminar de creanțe",
  creditorsMeetingMinutes: "Proces-verbal AGC confirmare lichidator",
  definitiveClaimsTable: "Tabel definitiv de creanțe",
  finalReportArt167: "Raport final (Art. 167)",
  creditorNotificationHtml: "Notificare deschidere procedură (HTML)",
};

/** Incoming document friendly names (received from court / external parties). */
export const INCOMING_DOCUMENT_LABELS: Record<IncomingDocumentType, string> = {
  CourtOpeningDecision: "Sentință / Hotărâre deschidere procedură",
  BpiPublication: "Publicație BPI",
  CreditorNotification: "Notificare creditori",
  CreditorClaim: "Cerere de creanță",
  AssetInventory: "Inventar active",
  PractitionerReport: "Raport practician",
  FinancialStatement: "Situație financiară",
  TaxCertificate: "Certificat fiscal",
  BankStatement: "Extras de cont",
  LiquidationReport: "Raport de lichidare",
  Generated: "Document generat",
  Other: "Alt document",
};

export const INCOMING_DOCUMENT_DESC: Record<IncomingDocumentType, string> = {
  CourtOpeningDecision:
    "Document emis de instanță care deschide procedura de insolvență. " +
    "Încarcă un exemplu PDF — sistemul îl va analiza și va recunoaște automat documentele similare " +
    "încărcate de practicieni ca notificare de deschidere a procedurii.",
  BpiPublication: "Publicație oficială în Buletinul Procedurilor de Insolvență.",
  CreditorNotification: "Notificare transmisă creditorilor privind deschiderea sau derularea procedurii.",
  CreditorClaim: "Cerere de înscriere a creanței depusă de un creditor.",
  AssetInventory: "Inventar al bunurilor debitorului întocmit de practician.",
  PractitionerReport: "Raport periodic al practicianului în insolvență.",
  FinancialStatement: "Situație financiară (bilanț, cont de profit și pierdere etc.).",
  TaxCertificate: "Certificat de atestare fiscală emis de ANAF.",
  BankStatement: "Extras de cont bancar al debitorului.",
  LiquidationReport: "Raport final de lichidare și distribuire a fondurilor.",
  Generated: "Document generat automat de sistem.",
  Other: "Alt tip de document care nu se încadrează în categoriile de mai sus.",
};

type UiLocale = "en" | "ro" | "hu";

const INCOMING_DOCUMENT_LABELS_BY_LOCALE: Record<UiLocale, Record<IncomingDocumentType, string>> = {
  en: {
    CourtOpeningDecision: "Court opening decision",
    BpiPublication: "BPI publication",
    CreditorNotification: "Creditor notification",
    CreditorClaim: "Creditor claim",
    AssetInventory: "Asset inventory",
    PractitionerReport: "Practitioner report",
    FinancialStatement: "Financial statement",
    TaxCertificate: "Tax certificate",
    BankStatement: "Bank statement",
    LiquidationReport: "Liquidation report",
    Generated: "Generated document",
    Other: "Other document",
  },
  ro: {
    CourtOpeningDecision: "Sentință / Hotărâre deschidere procedură",
    BpiPublication: "Publicație BPI",
    CreditorNotification: "Notificare creditori",
    CreditorClaim: "Cerere de creanță",
    AssetInventory: "Inventar active",
    PractitionerReport: "Raport practician",
    FinancialStatement: "Situație financiară",
    TaxCertificate: "Certificat fiscal",
    BankStatement: "Extras de cont",
    LiquidationReport: "Raport de lichidare",
    Generated: "Document generat",
    Other: "Alt document",
  },
  hu: {
    CourtOpeningDecision: "Eljárásmegnyitó bírósági határozat",
    BpiPublication: "BPI közzététel",
    CreditorNotification: "Hitelezői értesítés",
    CreditorClaim: "Követelés bejelentése",
    AssetInventory: "Vagyonleltár",
    PractitionerReport: "Szakértői jelentés",
    FinancialStatement: "Pénzügyi kimutatás",
    TaxCertificate: "Adóigazolás",
    BankStatement: "Bankszámlakivonat",
    LiquidationReport: "Felszámolási jelentés",
    Generated: "Generált dokumentum",
    Other: "Egyéb dokumentum",
  },
};

const INCOMING_DOCUMENT_DESC_BY_LOCALE: Record<UiLocale, Record<IncomingDocumentType, string>> = {
  en: {
    CourtOpeningDecision:
      "Court-issued document that opens the insolvency procedure. " +
      "Upload one sample PDF and AI will learn to auto-recognize and classify similar documents uploaded later.",
    BpiPublication: "Official publication in the Insolvency Procedures Bulletin.",
    CreditorNotification: "Notification sent to creditors about the opening or progress of the procedure.",
    CreditorClaim: "Creditor's claim registration request.",
    AssetInventory: "Inventory of the debtor's assets prepared by the practitioner.",
    PractitionerReport: "Periodic report issued by the insolvency practitioner.",
    FinancialStatement: "Financial statement (balance sheet, P&L, etc.).",
    TaxCertificate: "Tax compliance certificate issued by the tax authority.",
    BankStatement: "Bank account statement of the debtor.",
    LiquidationReport: "Final liquidation and fund distribution report.",
    Generated: "Document automatically generated by the system.",
    Other: "Other document type not covered by the above categories.",
  },
  ro: {
    CourtOpeningDecision:
      "Document emis de instanță care deschide procedura de insolvență. " +
      "Încarcă un exemplu PDF și AI-ul va recunoaște și clasifica automat documentele similare încărcate ulterior.",
    BpiPublication: "Publicație oficială în Buletinul Procedurilor de Insolvență.",
    CreditorNotification: "Notificare transmisă creditorilor privind deschiderea sau derularea procedurii.",
    CreditorClaim: "Cerere de înscriere a creanței depusă de un creditor.",
    AssetInventory: "Inventar al bunurilor debitorului întocmit de practician.",
    PractitionerReport: "Raport periodic al practicianului în insolvență.",
    FinancialStatement: "Situație financiară (bilanț, cont de profit și pierdere etc.).",
    TaxCertificate: "Certificat de atestare fiscală emis de ANAF.",
    BankStatement: "Extras de cont bancar al debitorului.",
    LiquidationReport: "Raport final de lichidare și distribuire a fondurilor.",
    Generated: "Document generat automat de sistem.",
    Other: "Alt tip de document care nu se încadrează în categoriile de mai sus.",
  },
  hu: {
    CourtOpeningDecision:
      "A bíróság által kibocsátott dokumentum, amely megnyitja a fizetésképtelenségi eljárást. " +
      "Töltsön fel egy mint PDF-et, és az AI később automatikusan felismeri és osztályozza a hasonló dokumentumokat.",
    BpiPublication: "Hivatalos közzététel a Fizetésképtelenségi Eljárások Értesítőjében.",
    CreditorNotification: "Az eljárás megnyitásáról vagy menetéről szóló hitelezői értesítés.",
    CreditorClaim: "Hitelező által benyújtott követelésbejelentési kérelem.",
    AssetInventory: "Az adós vagyonáról a szakértő által készített leltár.",
    PractitionerReport: "A felszámoló biztos rendszeres időközönként kiadott jelentése.",
    FinancialStatement: "Pénzügyi kimutatás (mérleg, eredménykimutatás stb.).",
    TaxCertificate: "Az adóhatóság által kiállított adóigazolás.",
    BankStatement: "Az adós bankszámlájának kivonata.",
    LiquidationReport: "Záró felszámolási és vagyonfelosztási jelentés.",
    Generated: "A rendszer által automatikusan generált dokumentum.",
    Other: "Más dokumentumtípus, amely nem tartozik a fenti kategóriákba.",
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
  creditorNotificationBpi: "Deschidere procedură",
  reportArt97: "Observație",
  mandatoryReport: "Monitorizare",
  preliminaryClaimsTable: "Verificare creanțe",
  creditorsMeetingMinutes: "Adunarea creditorilor",
  definitiveClaimsTable: "Lichidare",
  finalReportArt167: "Închidere",
  creditorNotificationHtml: "Deschidere procedură",
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

  /** Upload a .docx Word document, extract HTML, and insert AI-detected placeholders. */
  importWordDocument: (file: File, onProgress?: (pct: number) => void) => {
    const form = new FormData();
    form.append("file", file);
    // Do NOT set Content-Type manually — axios must auto-generate it with the multipart boundary.
    return client.post<ImportWordDocumentResult>(
      "/document-templates/import-word",
      form,
      {
        onUploadProgress: onProgress
          ? (e) => { if (e.total) onProgress(Math.round((e.loaded / e.total) * 100)); }
          : undefined,
      },
    );
  },

  /** Upload a sample/reference PDF for an incoming document type (AI recognition). */
  uploadIncomingReference: (type: IncomingDocumentType, file: File, onProgress?: (pct: number) => void) => {
    const form = new FormData();
    form.append("file", file);
    return client.post<{ profileId: string; type: string; fileName: string; fileSize: number; uploadedOn: string; message: string }>(
      `/document-templates/incoming-reference/${type}`,
      form,
      {
        onUploadProgress: onProgress
          ? (e) => { if (e.total) onProgress(Math.round((e.loaded / e.total) * 100)); }
          : undefined,
      },
    );
  },

  /** Check whether a reference PDF has been uploaded for an incoming document type. */
  getIncomingReference: (type: IncomingDocumentType) =>
    client.get<IncomingDocumentReferenceStatus>(`/document-templates/incoming-reference/${type}`),

  /** Returns the URL to stream the reference PDF (for PDF.js rendering). */
  getIncomingReferenceFileUrl: (type: IncomingDocumentType): string =>
    `/api/document-templates/incoming-reference/${type}/file`,

  /** Retrieve saved annotations for an incoming document type. */
  getIncomingAnnotations: (type: IncomingDocumentType) =>
    client.get<IncomingAnnotationsPayload>(`/document-templates/incoming-reference/${type}/annotations`),

  /** Persist annotation rectangles + optional notes for an incoming document type. */
  saveIncomingAnnotations: (type: IncomingDocumentType, payload: IncomingAnnotationsPayload) =>
    client.post(`/document-templates/incoming-reference/${type}/annotations`, payload),

  /** Retrieve the full DB profile (AI summaries, parameters, annotation count) for a document type. */
  getIncomingDocumentProfile: (type: IncomingDocumentType) =>
    client.get<IncomingDocumentProfile>(`/document-templates/incoming-reference/${type}/profile`),

  /** Trigger AI analysis: generates EN/RO/HU summaries + structured field parameters and saves to DB. */
  analyseIncomingDocument: (type: IncomingDocumentType) =>
    client.post<AiDocumentProfileResult>(`/document-templates/incoming-reference/${type}/analyse`),

  /** Ask AI to locate verbatim text for each annotatable field within the extracted document text. */
  suggestAnnotations: (type: IncomingDocumentType, extractedText: string) =>
    client.post<{ suggestions: Record<string, string>; aiConfigured: boolean; callFailed: boolean; errorMessage?: string }>(
      `/document-templates/incoming-reference/${type}/suggest-annotations`,
      { extractedText },
    ),

  // ── Per-profile (ID-based) operations ────────────────────────────────────

  /** List all training document profiles for a given type, newest first. */
  getIncomingProfilesForType: (type: IncomingDocumentType) =>
    client.get<IncomingDocumentProfileEntry[]>(`/document-templates/incoming-reference/${type}/all`),

  /** Returns the URL to stream the PDF for a specific profile by Id. */
  getIncomingProfileFileUrl: (profileId: string): string =>
    `/api/document-templates/incoming-reference/profile/${profileId}/file`,

  /** Retrieve saved annotations for a specific profile by Id. */
  getIncomingAnnotationsById: (profileId: string) =>
    client.get<IncomingAnnotationsPayload>(`/document-templates/incoming-reference/profile/${profileId}/annotations`),

  /** Persist annotation rectangles for a specific profile by Id. */
  saveIncomingAnnotationsById: (profileId: string, payload: IncomingAnnotationsPayload) =>
    client.post(`/document-templates/incoming-reference/profile/${profileId}/annotations`, payload),

  /** Get the full profile (including AI summaries) by Id. */
  getIncomingProfileById: (profileId: string) =>
    client.get<IncomingDocumentProfileById>(`/document-templates/incoming-reference/profile/${profileId}`),

  /** Ask AI for annotation suggestions for a specific profile. */
  suggestAnnotationsById: (profileId: string, extractedText: string) =>
    client.post<{ suggestions: Record<string, string>; aiConfigured: boolean; callFailed: boolean; errorMessage?: string }>(
      `/document-templates/incoming-reference/profile/${profileId}/suggest-annotations`,
      { extractedText },
    ),

  /** Finalize a training document and submit it for AI recognition training. Locks the profile. */
  finalizeAndTrain: (profileId: string) =>
    client.post<FinalizeAndTrainResult>(
      `/document-templates/incoming-reference/profile/${profileId}/finalize-and-train`,
    ),

  /** Delete a non-finalized training document profile. */
  deleteIncomingProfile: (profileId: string) =>
    client.delete(`/document-templates/incoming-reference/profile/${profileId}`),


  /** Convert arbitrary HTML (already rendered + optionally signed) to a PDF download.
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
