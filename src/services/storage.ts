import {
  collection,
  doc,
  getDocs,
  getDoc,
  setDoc,
  updateDoc,
  deleteDoc,
  query,
  orderBy,
} from 'firebase/firestore';
import { db } from './firebase';
import type { ContractCase, StorageProvider, User } from '../types';

const COLLECTION = 'cases';
const USER_KEY = 'insolvpoc_current_user';

const casesRef = collection(db, COLLECTION);

class FirestoreProvider implements StorageProvider {
  async getCases(): Promise<ContractCase[]> {
    const q = query(casesRef, orderBy('createdAt', 'desc'));
    const snapshot = await getDocs(q);
    return snapshot.docs.map((d) => d.data() as ContractCase);
  }

  async getCase(id: string): Promise<ContractCase | undefined> {
    const snap = await getDoc(doc(db, COLLECTION, id));
    return snap.exists() ? (snap.data() as ContractCase) : undefined;
  }

  async saveCase(contractCase: ContractCase): Promise<void> {
    await setDoc(doc(db, COLLECTION, contractCase.id), contractCase);
  }

  async updateCase(id: string, updates: Partial<ContractCase>): Promise<void> {
    await updateDoc(doc(db, COLLECTION, id), updates);
  }

  async deleteCase(id: string): Promise<void> {
    await deleteDoc(doc(db, COLLECTION, id));
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
