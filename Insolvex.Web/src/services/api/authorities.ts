import client from "./client";

interface AuthorityRecord {
  id: string;
  tenantId: string | null;
  name: string;
  locality?: string;
  county?: string;
  address?: string;
  postalCode?: string;
  phone?: string;
  fax?: string;
  email?: string;
  website?: string;
  contactPerson?: string;
  notes?: string;
  isGlobal: boolean;
  isTenantOverride: boolean;
  overridesGlobalId?: string;
  // Tribunal-specific
  section?: string;
  registryPhone?: string;
  registryFax?: string;
  registryEmail?: string;
  registryHours?: string;
  // FinanceAuthority/LocalGov-specific
  scheduleHours?: string;
}

const buildCrud = (basePath: string) => ({
  getAll: () => client.get<AuthorityRecord[]>(basePath),
  getById: (id: string) => client.get<AuthorityRecord>(`${basePath}/${id}`),
  create: (data: Record<string, unknown>) => client.post(basePath, data),
  update: (id: string, data: Record<string, unknown>) => client.put(`${basePath}/${id}`, data),
  delete: (id: string) => client.delete(`${basePath}/${id}`),
  importCsv: (file: File) => {
  const formData = new FormData();
    formData.append("file", file);
    return client.post<{ imported: number; errors: string[] }>(`${basePath}/import-csv`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  exportCsvUrl: `${basePath}/export-csv`,
});

export const tribunalsApi = buildCrud("/tribunals");
export const financeApi = buildCrud("/finance-authorities");
export const localGovApi = buildCrud("/local-governments");

export type { AuthorityRecord };
