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

  getHistory: (id: string, page = 0, pageSize = 50) =>
    client.get<{ items: import("./types").AuditLogDto[]; total: number }>(`/cases/${id}/history`, { params: { page, pageSize } }),

  changeProcedureType: (id: string, data: { newProcedureType: string; reason: string }) =>
    client.post<{ removedStages: string[]; addedStages: string[]; preservedTasks: number }>(`/cases/${id}/change-procedure-type`, data),

  getProcedureHistory: (id: string) =>
    client.get<{ id: string; oldProcedureType: string; newProcedureType: string; changedAt: string; changedByName: string | null; reason: string | null; workflowStagesRemovedJson: string | null }[]>(`/cases/${id}/procedure-history`),

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

  // Claims (Creditor Outlay)
  getClaims: (caseId: string) =>
    client.get<import("./types").CreditorClaimDto[]>(`/cases/${caseId}/claims`),

  createClaim: (caseId: string, data: Record<string, unknown>) =>
    client.post<import("./types").CreditorClaimDto>(`/cases/${caseId}/claims`, data),

  updateClaim: (caseId: string, claimId: string, data: Record<string, unknown>) =>
    client.put<import("./types").CreditorClaimDto>(`/cases/${caseId}/claims/${claimId}`, data),

  deleteClaim: (caseId: string, claimId: string) =>
    client.delete(`/cases/${caseId}/claims/${claimId}`),

  // Individual party (for non-company creditors)
  addIndividualParty: (caseId: string, data: Record<string, unknown>) =>
    client.post<import("./types").CasePartyDto>(`/cases/${caseId}/parties/individual`, data),

  // Export helpers (use raw fetch with auth for file downloads)
  exportCsvUrl: "/cases/export-csv",
  downloadZipUrl: (caseId: string) => `/cases/${caseId}/documents/download-zip`,
};
