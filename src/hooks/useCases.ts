import { useState, useCallback, useEffect } from "react";
import type { InsolvencyCase, InsolvencyDocument } from "../types";
import { storage } from "../services/storage";

export interface CaseWithDocuments {
  case: InsolvencyCase;
  documents: InsolvencyDocument[];
}

export function useCases() {
  const [cases, setCases] = useState<InsolvencyCase[]>([]);
  const [activeCaseId, setActiveCaseId] = useState<string | null>(null);
  const [activeCaseWithDocs, setActiveCaseWithDocs] =
    useState<CaseWithDocuments | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    const data = await storage.getInsolvencyCases();
    setCases(data);
  }, []);

  useEffect(() => {
    if (activeCaseId == null) {
      setActiveCaseWithDocs(null);
      return;
    }
    let cancelled = false;
    storage.getCaseWithDocuments(activeCaseId).then((pair) => {
      if (!cancelled && pair) {
        setActiveCaseWithDocs(pair);
      } else if (!cancelled) {
        setActiveCaseWithDocs(null);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [activeCaseId]);

  useEffect(() => {
    let cancelled = false;
    storage.getInsolvencyCases().then((data) => {
      if (!cancelled) {
        setCases(data);
      }
    }).catch((err) => {
      console.error("Failed to load insolvency cases from Firestore:", err);
    }).finally(() => {
      if (!cancelled) {
        setLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, []);

  const addCase = useCallback(
    async (insolvencyCase: InsolvencyCase) => {
      await storage.saveInsolvencyCase(insolvencyCase);
      await refresh();
      setActiveCaseId(insolvencyCase.id);
    },
    [refresh],
  );

  const updateCase = useCallback(
    async (id: string, updates: Partial<InsolvencyCase>) => {
      await storage.updateInsolvencyCase(id, updates);
      await refresh();
    },
    [refresh],
  );

  const deleteCase = useCallback(
    async (id: string) => {
      await storage.deleteInsolvencyCase(id);
      await refresh();
      if (activeCaseId === id) {
        setActiveCaseId(null);
      }
    },
    [activeCaseId, refresh],
  );

  const addDocumentToCase = useCallback(
    async (caseId: string, document: InsolvencyDocument) => {
      await storage.addDocumentToCase(caseId, document);
      await refresh();
      if (activeCaseId === caseId) {
        const pair = await storage.getCaseWithDocuments(caseId);
        setActiveCaseWithDocs(pair ?? null);
      }
    },
    [activeCaseId, refresh],
  );

  const updateDocument = useCallback(
    async (
      caseId: string,
      documentId: string,
      updates: Partial<InsolvencyDocument>,
    ) => {
      await storage.updateDocument(caseId, documentId, updates);
      if (activeCaseId === caseId) {
        const pair = await storage.getCaseWithDocuments(caseId);
        setActiveCaseWithDocs(pair ?? null);
      }
    },
    [activeCaseId],
  );

  const getCaseWithDocuments = useCallback(
    async (caseId: string) => storage.getCaseWithDocuments(caseId),
    [],
  );

  return {
    cases,
    activeCaseId,
    activeCaseWithDocs,
    setActiveCaseId,
    addCase,
    updateCase,
    deleteCase,
    addDocumentToCase,
    updateDocument,
    getCaseWithDocuments,
    refresh,
    loading,
  };
}
