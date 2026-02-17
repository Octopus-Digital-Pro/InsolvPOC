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

export interface ContractCase {
  // Internal / meta
  id: string;
  title: string;
  sourceFileName: string;
  createdAt: string;
  createdBy: string;

  // Per-field edit tracking: { fieldKey: { editedBy, editedAt } }
  edits?: Record<string, FieldEdit>;

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
  getCases(): ContractCase[];
  getCase(id: string): ContractCase | undefined;
  saveCase(contractCase: ContractCase): void;
  updateCase(id: string, updates: Partial<ContractCase>): void;
  deleteCase(id: string): void;
}
