export interface User {
  id: string;
  name: string;
  role: string;

  avatar: string;
}

export const USERS: User[] = [
  {
    id: "jon-doe",
    name: "Jon Doe",
    role: "Insolvency Practitioner",
    avatar: "https://i.pravatar.cc/150?img=7",
  },
  {
    id: "gipsz-jakab",
    name: "Gipsz Jakab",
    role: "Firm Admin",
    avatar: "https://i.pravatar.cc/150?img=50",
  },
];

export interface FieldEdit {
  editedBy: string;
  editedAt: string;
}

/** Single entry in case edit history (Jira-style activity). */
export interface EditHistoryEntry {
  at: string;
  by: string;
  field: string;
  oldValue?: string;
  newValue?: string;
}

export interface Company {
  id: string;
  name: string;
  cuiRo: string;
  address: string;
  assignedTo?: string;
  createdAt: string;
  createdBy?: string;
}

export type CompanyTaskStatus = "open" | "blocked" | "done";

export interface CompanyTask {
  id: string;
  companyId: string;
  title: string;
  description: string;
  /** Free-text labels (e.g. "urgent, review"). */
  labels?: string;
  deadline: string;
  status: CompanyTaskStatus;
  assignedTo?: string;
  createdAt?: string;
}

export interface ContractCase {
  id: string;
  title: string;
  sourceFileName: string;
  createdAt: string;
  createdBy: string;
  companyId?: string;

  // Per-field edit tracking: { fieldKey: { editedBy, editedAt } }
  edits?: Record<string, FieldEdit>;
  // Timeline of edits for activity/history (newest first when displayed)
  editHistory?: EditHistoryEntry[];

  // Parties
  beneficiary: string;
  beneficiaryAddress: string;
  beneficiaryIdentifiers: string;
  contractor: string;
  contractorAddress: string;
  contractorIdentifiers: string;
  subcontractors: string;

  // Contract core
  contractTitleOrSubject: string;
  contractNumberOrReference: string;
  procurementProcedure: string;
  cpvCodes: string;

  // Dates & period
  contractDate: string;
  effectiveDate: string;
  contractPeriod: string;

  // Signatures
  signatories: string;
  signingLocation: string;

  // Catch-all
  otherImportantClauses: string;

  // Raw AI output
  rawJson: string;
}

/** One uploaded insolvency document (one file) attached to a dosar case. */
export interface InsolvencyDocument {
  id: string;
  caseId: string;
  sourceFileName: string;
  uploadedAt: string;
  uploadedBy: string;
  docType: string;
  /** ISO date string or { text, iso } from extraction. */
  documentDate: string;
  /** Full extraction result; use InsolvencyExtractionResult from services/openai when reading. */
  rawExtraction: unknown;
}

/** One insolvency matter (dosar) – case number + court + debtor; holds multiple documents. */
export interface InsolvencyCase {
  id: string;
  caseNumber: string;
  courtName: string;
  debtorName: string;
  debtorCui: string;
  createdAt: string;
  createdBy: string;
  companyId?: string;
  assignedTo?: string;
}

export interface StorageProvider {
  getCases(): Promise<ContractCase[]>;
  getCase(id: string): Promise<ContractCase | undefined>;
  saveCase(contractCase: ContractCase): Promise<void>;
  updateCase(id: string, updates: Partial<ContractCase>): Promise<void>;
  deleteCase(id: string): Promise<void>;

  getInsolvencyCases(): Promise<InsolvencyCase[]>;
  getInsolvencyCase(id: string): Promise<InsolvencyCase | undefined>;
  /** Returns case plus its documents. */
  getCaseWithDocuments(caseId: string): Promise<{ case: InsolvencyCase; documents: InsolvencyDocument[] } | undefined>;
  saveInsolvencyCase(insolvencyCase: InsolvencyCase): Promise<void>;
  updateInsolvencyCase(id: string, updates: Partial<InsolvencyCase>): Promise<void>;
  deleteInsolvencyCase(id: string): Promise<void>;
  addDocumentToCase(caseId: string, document: InsolvencyDocument): Promise<void>;
  updateDocument(caseId: string, documentId: string, updates: Partial<InsolvencyDocument>): Promise<void>;
  getInsolvencyDocuments(caseId: string): Promise<InsolvencyDocument[]>;

  getCompanies(): Promise<Company[]>;
  getCompany(id: string): Promise<Company | undefined>;
  saveCompany(company: Company): Promise<void>;
  updateCompany(id: string, updates: Partial<Company>): Promise<void>;
  deleteCompany(id: string): Promise<void>;

  getTasks(): Promise<CompanyTask[]>;
  getTasksByCompany(companyId: string): Promise<CompanyTask[]>;
  getTask(id: string): Promise<CompanyTask | undefined>;
  saveTask(task: CompanyTask): Promise<void>;
  updateTask(id: string, updates: Partial<CompanyTask>): Promise<void>;
  deleteTask(id: string): Promise<void>;
}
