import {
  collection,
  doc,
  getDocs,
  getDoc,
  setDoc,
  updateDoc,
  deleteDoc,
  deleteField,
  query,
  orderBy,
} from "firebase/firestore";
import { db } from "./firebase";
import type { Company, ContractCase, StorageProvider, User } from "../types";

const CASES_COLLECTION = "cases";
const COMPANIES_COLLECTION = "companies";
const USER_KEY = "insolvpoc_current_user";

const casesRef = collection(db, CASES_COLLECTION);
const companiesRef = collection(db, COMPANIES_COLLECTION);

class FirestoreProvider implements StorageProvider {
  async getCases(): Promise<ContractCase[]> {
    const q = query(casesRef, orderBy("createdAt", "desc"));
    const snapshot = await getDocs(q);
    return snapshot.docs.map((d) => d.data() as ContractCase);
  }

  async getCase(id: string): Promise<ContractCase | undefined> {
    const snap = await getDoc(doc(db, CASES_COLLECTION, id));
    return snap.exists() ? (snap.data() as ContractCase) : undefined;
  }

  async saveCase(contractCase: ContractCase): Promise<void> {
    await setDoc(doc(db, CASES_COLLECTION, contractCase.id), contractCase);
  }

  async updateCase(id: string, updates: Partial<ContractCase>): Promise<void> {
    const payload: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined) {
        payload[key] = deleteField();
      } else {
        payload[key] = value;
      }
    }
    await updateDoc(doc(db, CASES_COLLECTION, id), payload);
  }

  async deleteCase(id: string): Promise<void> {
    await deleteDoc(doc(db, CASES_COLLECTION, id));
  }

  async getCompanies(): Promise<Company[]> {
    const q = query(companiesRef, orderBy("name"));
    const snapshot = await getDocs(q);
    return snapshot.docs.map((d) => d.data() as Company);
  }

  async getCompany(id: string): Promise<Company | undefined> {
    const snap = await getDoc(doc(db, COMPANIES_COLLECTION, id));
    return snap.exists() ? (snap.data() as Company) : undefined;
  }

  async saveCompany(company: Company): Promise<void> {
    await setDoc(doc(db, COMPANIES_COLLECTION, company.id), company);
  }

  async updateCompany(id: string, updates: Partial<Company>): Promise<void> {
    const payload: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined) {
        payload[key] = deleteField();
      } else {
        payload[key] = value;
      }
    }
    await updateDoc(doc(db, COMPANIES_COLLECTION, id), payload);
  }

  async deleteCompany(id: string): Promise<void> {
    await deleteDoc(doc(db, COMPANIES_COLLECTION, id));
  }
}

export const storage: StorageProvider = new FirestoreProvider();

// User session helpers (stay in localStorage – no Firebase Auth needed for POC)

export function getCurrentUser(): User | null {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function setCurrentUser(user: User): void {
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function clearCurrentUser(): void {
  localStorage.removeItem(USER_KEY);
}
