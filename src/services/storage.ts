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
  where,
} from "firebase/firestore";
import { db } from "./firebase";
import type {
  Company,
  CompanyTask,
  ContractCase,
  InsolvencyCase,
  InsolvencyDocument,
  StorageProvider,
  User,
} from "../types";

const CASES_COLLECTION = "cases";
const INSOLVENCY_CASES_COLLECTION = "insolvencyCases";
const INSOLVENCY_DOCUMENTS_COLLECTION = "insolvencyDocuments";
const COMPANIES_COLLECTION = "companies";
const TASKS_COLLECTION = "tasks";
const USER_KEY = "insolvpoc_current_user";

const casesRef = collection(db, CASES_COLLECTION);
const insolvencyCasesRef = collection(db, INSOLVENCY_CASES_COLLECTION);
const insolvencyDocumentsRef = collection(db, INSOLVENCY_DOCUMENTS_COLLECTION);
const companiesRef = collection(db, COMPANIES_COLLECTION);
const tasksRef = collection(db, TASKS_COLLECTION);

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

  async getInsolvencyCases(): Promise<InsolvencyCase[]> {
    const q = query(
      insolvencyCasesRef,
      orderBy("createdAt", "desc"),
    );
    const snapshot = await getDocs(q);
    return snapshot.docs.map((d) => d.data() as InsolvencyCase);
  }

  async getInsolvencyCase(id: string): Promise<InsolvencyCase | undefined> {
    const snap = await getDoc(doc(db, INSOLVENCY_CASES_COLLECTION, id));
    return snap.exists() ? (snap.data() as InsolvencyCase) : undefined;
  }

  async getCaseWithDocuments(
    caseId: string,
  ): Promise<{ case: InsolvencyCase; documents: InsolvencyDocument[] } | undefined> {
    const insolvencyCase = await this.getInsolvencyCase(caseId);
    if (!insolvencyCase) return undefined;
    const documents = await this.getInsolvencyDocuments(caseId);
    return { case: insolvencyCase, documents };
  }

  async saveInsolvencyCase(insolvencyCase: InsolvencyCase): Promise<void> {
    await setDoc(
      doc(db, INSOLVENCY_CASES_COLLECTION, insolvencyCase.id),
      insolvencyCase,
    );
  }

  async updateInsolvencyCase(
    id: string,
    updates: Partial<InsolvencyCase>,
  ): Promise<void> {
    const payload: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined) {
        payload[key] = deleteField();
      } else {
        payload[key] = value;
      }
    }
    await updateDoc(doc(db, INSOLVENCY_CASES_COLLECTION, id), payload);
  }

  async deleteInsolvencyCase(id: string): Promise<void> {
    const documents = await this.getInsolvencyDocuments(id);
    for (const docEntry of documents) {
      await deleteDoc(
        doc(db, INSOLVENCY_DOCUMENTS_COLLECTION, docEntry.id),
      );
    }
    await deleteDoc(doc(db, INSOLVENCY_CASES_COLLECTION, id));
  }

  async addDocumentToCase(
    caseId: string,
    document: InsolvencyDocument,
  ): Promise<void> {
    const toSave = { ...document, caseId };
    await setDoc(
      doc(db, INSOLVENCY_DOCUMENTS_COLLECTION, document.id),
      toSave,
    );
  }

  async getInsolvencyDocuments(caseId: string): Promise<InsolvencyDocument[]> {
    const q = query(
      insolvencyDocumentsRef,
      where("caseId", "==", caseId),
    );
    const snapshot = await getDocs(q);
    const docs = snapshot.docs.map((d) => d.data() as InsolvencyDocument);
    docs.sort(
      (a, b) =>
        new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime(),
    );
    return docs;
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

  async getTasks(): Promise<CompanyTask[]> {
    const snapshot = await getDocs(tasksRef);
    const tasks = snapshot.docs.map((d) => d.data() as CompanyTask);
    return tasks.sort((a, b) => {
      const da = a.deadline ? new Date(a.deadline).getTime() : Infinity;
      const db = b.deadline ? new Date(b.deadline).getTime() : Infinity;
      return da - db;
    });
  }

  async getTasksByCompany(companyId: string): Promise<CompanyTask[]> {
    const snapshot = await getDocs(tasksRef);
    const tasks = snapshot.docs
      .map((d) => d.data() as CompanyTask)
      .filter((t) => t.companyId === companyId);
    return tasks.sort((a, b) => {
      const da = a.deadline ? new Date(a.deadline).getTime() : Infinity;
      const db = b.deadline ? new Date(b.deadline).getTime() : Infinity;
      return da - db;
    });
  }

  async getTask(id: string): Promise<CompanyTask | undefined> {
    const snap = await getDoc(doc(db, TASKS_COLLECTION, id));
    return snap.exists() ? (snap.data() as CompanyTask) : undefined;
  }

  async saveTask(task: CompanyTask): Promise<void> {
    await setDoc(doc(db, TASKS_COLLECTION, task.id), {
      ...task,
      createdAt: task.createdAt ?? new Date().toISOString(),
    });
  }

  async updateTask(id: string, updates: Partial<CompanyTask>): Promise<void> {
    const payload: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined) {
        payload[key] = deleteField();
      } else {
        payload[key] = value;
      }
    }
    await updateDoc(doc(db, TASKS_COLLECTION, id), payload);
  }

  async deleteTask(id: string): Promise<void> {
    await deleteDoc(doc(db, TASKS_COLLECTION, id));
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
