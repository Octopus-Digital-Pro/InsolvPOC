import { useState, useCallback, useEffect } from 'react';
import type { ContractCase } from '../types';
import { storage } from '../services/storage';

export function useCases() {
  const [cases, setCases] = useState<ContractCase[]>([]);
  const [activeCaseId, setActiveCaseId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const activeCase = cases.find((c) => c.id === activeCaseId) ?? null;

  const refresh = useCallback(async () => {
    const data = await storage.getCases();
    setCases(data);
  }, []);

  // Initial load
  useEffect(() => {
    let cancelled = false;
    storage.getCases().then((data) => {
      if (!cancelled) {
        setCases(data);
        setLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, []);

  const addCase = useCallback(
    async (contractCase: ContractCase) => {
      await storage.saveCase(contractCase);
      await refresh();
      setActiveCaseId(contractCase.id);
    },
    [refresh]
  );

  const updateCase = useCallback(
    async (id: string, updates: Partial<ContractCase>) => {
      await storage.updateCase(id, updates);
      await refresh();
    },
    [refresh]
  );

  const deleteCase = useCallback(
    async (id: string) => {
      await storage.deleteCase(id);
      await refresh();
      if (activeCaseId === id) {
        setActiveCaseId(null);
      }
    },
    [activeCaseId, refresh]
  );

  return {
    cases,
    activeCase,
    activeCaseId,
    setActiveCaseId,
    addCase,
    updateCase,
    deleteCase,
    loading,
  };
}
