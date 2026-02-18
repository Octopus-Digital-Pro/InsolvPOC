import { useState, useCallback, useEffect } from 'react';
import type { Company } from '../types';
import { storage } from '../services/storage';

export function useCompanies() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    const data = await storage.getCompanies();
    setCompanies(data);
  }, []);

  useEffect(() => {
    let cancelled = false;
    storage.getCompanies().then((data) => {
      if (!cancelled) {
        setCompanies(data);
      }
    }).catch((err) => {
      console.error('Failed to load companies from Firestore:', err);
    }).finally(() => {
      if (!cancelled) {
        setLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, []);

  const addCompany = useCallback(
    async (company: Company) => {
      await storage.saveCompany(company);
      await refresh();
    },
    [refresh]
  );

  const updateCompany = useCallback(
    async (id: string, updates: Partial<Company>) => {
      await storage.updateCompany(id, updates);
      await refresh();
    },
    [refresh]
  );

  const deleteCompany = useCallback(
    async (id: string) => {
      await storage.deleteCompany(id);
      await refresh();
    },
    [refresh]
  );

  return {
    companies,
    refresh,
    addCompany,
    updateCompany,
    deleteCompany,
    loading: loading,
  };
}
