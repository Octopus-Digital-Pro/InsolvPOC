import client from "./client";
import type { UserDto, TenantDto, AuditLogListResponse, AuditLogStats } from "./types";

export const usersApi = {
  getAll: () => client.get<UserDto[]>("/users"),
  getById: (id: string) => client.get<UserDto>(`/users/${id}`),
  update: (id: string, data: Partial<UserDto>) => client.put<UserDto>(`/users/${id}`, data),
  delete: (id: string) => client.delete(`/users/${id}`),
  invite: (data: { email: string; firstName: string; lastName: string; role: string }) =>
    client.post("/users/invite", data),
};

export const tenantsApi = {
  getAll: () => client.get<TenantDto[]>("/tenants"),
  getById: (id: string) => client.get<TenantDto>(`/tenants/${id}`),
  create: (data: { name: string; domain?: string; planName?: string; region?: string; isDemo?: boolean }) => client.post<TenantDto>("/tenants", data),
  update: (id: string, data: Partial<TenantDto> & { region?: string }) => client.put<TenantDto>(`/tenants/${id}`, data),
};

export const auditLogsApi = {
  getAll: (params?: Record<string, string | number | undefined>) =>
    client.get<AuditLogListResponse>("/auditlogs", { params }),
  getCount: (params?: Record<string, string | number | undefined>) =>
    client.get<{ count: number }>("/auditlogs/count", { params }),
  getCategories: () => client.get<string[]>("/auditlogs/categories"),
  getStats: (params?: { from?: string; to?: string }) =>
    client.get<AuditLogStats>("/auditlogs/stats", { params }),
  export: (params?: Record<string, string | number | undefined>) =>
    client.get<Blob>("/auditlogs/export", { params, responseType: "blob" }),
};
