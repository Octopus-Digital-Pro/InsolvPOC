import client from "./client";
import type { DocumentDto } from "./types";

export const documentsApi = {
  getById: (id: string) =>
    client.get<DocumentDto>(`/documents/${id}`),

  create: (data: { caseId: string; sourceFileName: string; docType: string; documentDate?: string; rawExtraction?: string }) =>
    client.post<DocumentDto>("/documents", data),

  update: (id: string, data: Partial<{ docType: string; documentDate: string; rawExtraction: string }>) =>
    client.put<DocumentDto>(`/documents/${id}`, data),

  delete: (id: string) =>
    client.delete(`/documents/${id}`),

  getByCompany: (companyId: string) =>
  client.get<DocumentDto[]>(`/documents/by-company/${companyId}`),
};
