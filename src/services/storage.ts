import type { ContractCase, StorageProvider, User } from '../types';

const STORAGE_KEY = 'insolvpoc_cases';
const USER_KEY = 'insolvpoc_current_user';

class LocalStorageProvider implements StorageProvider {
  private readAll(): ContractCase[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  private writeAll(cases: ContractCase[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(cases));
  }

  getCases(): ContractCase[] {
    return this.readAll().sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
  }

  getCase(id: string): ContractCase | undefined {
    return this.readAll().find((c) => c.id === id);
  }

  saveCase(contractCase: ContractCase): void {
    const cases = this.readAll();
    cases.push(contractCase);
    this.writeAll(cases);
  }

  updateCase(id: string, updates: Partial<ContractCase>): void {
    const cases = this.readAll();
    const idx = cases.findIndex((c) => c.id === id);
    if (idx !== -1) {
      cases[idx] = { ...cases[idx], ...updates };
      this.writeAll(cases);
    }
  }

  deleteCase(id: string): void {
    const cases = this.readAll().filter((c) => c.id !== id);
    this.writeAll(cases);
  }
}

export const storage: StorageProvider = new LocalStorageProvider();

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
