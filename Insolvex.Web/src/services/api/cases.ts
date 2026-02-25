import client from "./client";
import type { CaseDto, CasePartyDto, CasePhaseDto, DocumentDto } from "./types";

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

  // Phases
  getPhases: (caseId: string) =>
    client.get<CasePhaseDto[]>(`/cases/${caseId}/phases`),

  initializePhases: (caseId: string) =>
    client.post<CasePhaseDto[]>(`/cases/${caseId}/phases/initialize`),

  updatePhase: (caseId: string, phaseId: string, data: Partial<CasePhaseDto>) =>
    client.put<CasePhaseDto>(`/cases/${caseId}/phases/${phaseId}`, data),

  advancePhase: (caseId: string) =>
    client.post<CasePhaseDto[]>(`/cases/${caseId}/phases/advance`),

  getPhaseRequirements: (caseId: string, phaseId: string) =>
    client.get(`/cases/${caseId}/phases/${phaseId}/requirements`),

  generatePhaseTasks: (caseId: string, phaseId: string) =>
    client.post<{ tasksGenerated: number; message: string }>(`/cases/${caseId}/phases/${phaseId}/generate-tasks`),

  // Export helpers (use raw fetch with auth for file downloads)
  exportCsvUrl: "/cases/export-csv",
  downloadZipUrl: (caseId: string) => `/cases/${caseId}/documents/download-zip`,
};
