import client from "./client";

export interface ONRCFirmResult {
  id: string;
  cui: string;
  name: string;
  tradeRegisterNo: string | null;
  caen: string | null;
  address: string | null;
  locality: string | null;
  county: string | null;
  postalCode: string | null;
  phone: string | null;
  status: string | null;
  incorporationYear: string | null;
  shareCapitalRon: number | null;
  region: string;
}

export interface ONRCImportResult {
  totalRows: number;
  imported: number;
  updated: number;
  skipped: number;
  errors: string[];
}

export interface ONRCDatabaseStats {
  region: string;
  totalRecords: number;
  lastImportedAt: string | null;
}

export const onrcApi = {
  search: (q: string, region = "Romania", maxResults = 10) =>
    client.get<ONRCFirmResult[]>("/onrc/search", { params: { q, region, maxResults } }),

  searchByCui: (cui: string, region = "Romania") =>
    client.get<ONRCFirmResult[]>("/onrc/search/cui", { params: { cui, region } }),

  searchByName: (name: string, region = "Romania") =>
client.get<ONRCFirmResult[]>("/onrc/search/name", { params: { name, region } }),

  importCsv: (file: File, region = "Romania") => {
    const formData = new FormData();
    formData.append("file", file);
    // Do NOT set Content-Type manually - axios must auto-set it with the correct multipart boundary
    return client.post<ONRCImportResult>(`/onrc/import?region=${encodeURIComponent(region)}`, formData);
  },

  getStats: (region = "Romania") =>
    client.get<ONRCDatabaseStats>("/onrc/stats", { params: { region } }),
};
