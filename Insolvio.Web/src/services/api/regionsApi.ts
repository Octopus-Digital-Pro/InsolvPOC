import client from "./client";
import type { RegionDto } from "./types";

export const regionsApi = {
  getAll: () =>
    client.get<RegionDto[]>("/regions"),

  create: (data: { name: string; isoCode: string; flag: string }) =>
    client.post<RegionDto>("/regions", data),

  delete: (id: string) =>
    client.delete(`/regions/${id}`),

  setDefault: (id: string) =>
    client.patch<RegionDto>(`/regions/${id}/set-default`),
};
