import type { InsolvencyNote, StorageProvider, User } from '../types';

const STORAGE_KEY = 'insolvpoc_notes';
const USER_KEY = 'insolvpoc_current_user';

class LocalStorageProvider implements StorageProvider {
  private readAll(): InsolvencyNote[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  private writeAll(notes: InsolvencyNote[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(notes));
  }

  getNotes(): InsolvencyNote[] {
    return this.readAll().sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
  }

  getNote(id: string): InsolvencyNote | undefined {
    return this.readAll().find((n) => n.id === id);
  }

  saveNote(note: InsolvencyNote): void {
    const notes = this.readAll();
    notes.push(note);
    this.writeAll(notes);
  }

  updateNote(id: string, updates: Partial<InsolvencyNote>): void {
    const notes = this.readAll();
    const idx = notes.findIndex((n) => n.id === id);
    if (idx !== -1) {
      notes[idx] = { ...notes[idx], ...updates };
      this.writeAll(notes);
    }
  }

  deleteNote(id: string): void {
    const notes = this.readAll().filter((n) => n.id !== id);
    this.writeAll(notes);
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
