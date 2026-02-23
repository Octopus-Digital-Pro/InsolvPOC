import client from "./client";
import type { DashboardDto } from "./types";

export const dashboardApi = {
  get: () => client.get<DashboardDto>("/dashboard"),
};
