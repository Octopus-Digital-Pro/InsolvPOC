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

export interface InsolvencyNote {
  id: string;
  title: string;
  companyName: string;
  addressee: string;
  dateAndDeadlines: string;
  court: string;
  rawExtractedText: string;
  sourceFileName: string;
  createdAt: string;
  createdBy: string;
}

export interface ExtractionResult {
  companyName: string;
  addressee: string;
  dateAndDeadlines: string;
  court: string;
  rawText: string;
}

export interface StorageProvider {
  getNotes(): InsolvencyNote[];
  getNote(id: string): InsolvencyNote | undefined;
  saveNote(note: InsolvencyNote): void;
  updateNote(id: string, updates: Partial<InsolvencyNote>): void;
  deleteNote(id: string): void;
}
