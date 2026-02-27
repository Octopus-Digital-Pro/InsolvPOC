import client from "./client";
import type { CaseDto, CasePartyDto, DocumentDto } from "./types";

export const casesApi = {
  getAll: (companyId?: string) =>
    client.get<CaseDto[]>("/cases", { params: companyId ? { companyId } : undefined }),

  getById: (id: string) =>
    client.get<CaseDto>(`/cases/${id}`),

  getDocuments: (caseId: string) =>
    client.get<DocumentDto[]>(`/cases/${caseId}/documents`),

  create: (data: Partial<CaseDto>) =>
    client.post<CaseDto>("/cases", data),

  update: (id: string, data: Partial<CaseDto>) =>
    client.put<CaseDto>(`/cases/${id}`, data),

  delete: (id: string) =>
    client.delete(`/cases/${id}`),

  // Parties
  getParties: (caseId: string) =>
    client.get<CasePartyDto[]>(`/cases/${caseId}/parties`),

  addParty: (caseId: string, data: Partial<CasePartyDto>) =>
    client.post<CasePartyDto>(`/cases/${caseId}/parties`, data),

  updateParty: (caseId: string, partyId: string, data: Partial<CasePartyDto>) =>
  client.put<CasePartyDto>(`/cases/${caseId}/parties/${partyId}`, data),

  removeParty: (caseId: string, partyId: string) =>
    client.delete(`/cases/${caseId}/parties/${partyId}`),

  // Assets
  getAssets: (caseId: string) =>
    client.get<import("./types").AssetDto[]>(`/cases/${caseId}/assets`),

  createAsset: (caseId: string, data: Record<string, unknown>) =>
    client.post<import("./types").AssetDto>(`/cases/${caseId}/assets`, data),

  updateAsset: (caseId: string, assetId: string, data: Record<string, unknown>) =>
    client.put<import("./types").AssetDto>(`/cases/${caseId}/assets/${assetId}`, data),

  deleteAsset: (caseId: string, assetId: string) =>
    client.delete(`/cases/${caseId}/assets/${assetId}`),

  // Export helpers (use raw fetch with auth for file downloads)
  exportCsvUrl: "/cases/export-csv",
  downloadZipUrl: (caseId: string) => `/cases/${caseId}/documents/download-zip`,
};
