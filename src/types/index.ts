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

export interface ContractCase {
  id: string;
  title: string;
  sourceFileName: string;
  createdAt: string;
  createdBy: string;
  companyId?: string;
  alertAt?: string;

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

export interface StorageProvider {
  getCases(): Promise<ContractCase[]>;
  getCase(id: string): Promise<ContractCase | undefined>;
  saveCase(contractCase: ContractCase): Promise<void>;
  updateCase(id: string, updates: Partial<ContractCase>): Promise<void>;
  deleteCase(id: string): Promise<void>;

  getCompanies(): Promise<Company[]>;
  getCompany(id: string): Promise<Company | undefined>;
  saveCompany(company: Company): Promise<void>;
  updateCompany(id: string, updates: Partial<Company>): Promise<void>;
  deleteCompany(id: string): Promise<void>;
}
