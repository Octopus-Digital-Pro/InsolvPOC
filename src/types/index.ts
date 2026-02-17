export interface User {
  id: string;
  name: string;
  initials: string;
  avatar: string;
}

export const USERS: User[] = [
  {
    id: "jon-doe",
    name: "Jon Doe",
    initials: "JD",
    avatar: "https://i.pravatar.cc/150?img=50",
  },
  {
    id: "gipsz-jakab",
    name: "Gipsz Jakab",
    initials: "GJ",
    avatar: "https://i.pravatar.cc/150?img=7",
  },
];

export interface ContractCase {
  // Internal / meta
  id: string;
  title: string;
  sourceFileName: string;
  createdAt: string;
  createdBy: string;

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
