import { useState, useCallback } from 'react';
import type { ContractCase } from '../types';
import { storage } from '../services/storage';

export function useCases() {
  const [cases, setCases] = useState<ContractCase[]>(() => storage.getCases());
  const [activeCaseId, setActiveCaseId] = useState<string | null>(null);

  const activeCase = cases.find((c) => c.id === activeCaseId) ?? null;

  const refresh = useCallback(() => {
    setCases(storage.getCases());
  }, []);

  const addCase = useCallback(
    (contractCase: ContractCase) => {
      storage.saveCase(contractCase);
      refresh();
      setActiveCaseId(contractCase.id);
    },
    [refresh]
  );

  const updateCase = useCallback(
    (id: string, updates: Partial<ContractCase>) => {
      storage.updateCase(id, updates);
      refresh();
    },
    [refresh]
  );

  const deleteCase = useCallback(
    (id: string) => {
      storage.deleteCase(id);
      refresh();
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
  };
}
