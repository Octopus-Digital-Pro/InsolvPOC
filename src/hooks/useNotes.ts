import { useState, useCallback } from 'react';
import type { InsolvencyNote } from '../types';
import { storage } from '../services/storage';

export function useNotes() {
  const [notes, setNotes] = useState<InsolvencyNote[]>(() => storage.getNotes());
  const [activeNoteId, setActiveNoteId] = useState<string | null>(null);

  const activeNote = notes.find((n) => n.id === activeNoteId) ?? null;

  const refresh = useCallback(() => {
    setNotes(storage.getNotes());
  }, []);

  const addNote = useCallback(
    (note: InsolvencyNote) => {
      storage.saveNote(note);
      refresh();
      setActiveNoteId(note.id);
    },
    [refresh]
  );

  const updateNote = useCallback(
    (id: string, updates: Partial<InsolvencyNote>) => {
      storage.updateNote(id, updates);
      refresh();
    },
    [refresh]
  );

  const deleteNote = useCallback(
    (id: string) => {
      storage.deleteNote(id);
      refresh();
      if (activeNoteId === id) {
        setActiveNoteId(null);
      }
    },
    [activeNoteId, refresh]
  );

  return {
    notes,
    activeNote,
    activeNoteId,
    setActiveNoteId,
    addNote,
    updateNote,
    deleteNote,
  };
}
