import client from "./client";
import type { CompanyDto, CompanyCasePartyDto } from "./types";

export const companiesApi = {
  getAll: (type?: string) =>
    client.get<CompanyDto[]>("/companies", { params: type ? { type } : undefined }),

  search: (q: string, maxResults = 10) =>
    client.get<CompanyDto[]>("/companies/search", { params: { q, maxResults } }),

  getById: (id: string) =>
    client.get<CompanyDto>(`/companies/${id}`),

  create: (data: Partial<CompanyDto>) =>
    client.post<CompanyDto>("/companies", data),

  update: (id: string, data: Partial<CompanyDto>) =>
    client.put<CompanyDto>(`/companies/${id}`, data),

  delete: (id: string) =>
    client.delete(`/companies/${id}`),

  exportCsvUrl: "/companies/export-csv",

  getPartiesByCompany: (companyId: string) =>
    client.get<CompanyCasePartyDto[]>(`/companies/${companyId}/parties`),
};
