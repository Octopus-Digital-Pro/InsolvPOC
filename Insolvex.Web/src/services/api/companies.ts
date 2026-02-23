import client from "./client";
import type { CompanyDto } from "./types";

export const companiesApi = {
  getAll: (type?: string) =>
    client.get<CompanyDto[]>("/companies", { params: type ? { type } : undefined }),

  getById: (id: string) =>
    client.get<CompanyDto>(`/companies/${id}`),

  create: (data: Partial<CompanyDto>) =>
    client.post<CompanyDto>("/companies", data),

  update: (id: string, data: Partial<CompanyDto>) =>
    client.put<CompanyDto>(`/companies/${id}`, data),

  delete: (id: string) =>
    client.delete(`/companies/${id}`),
};
