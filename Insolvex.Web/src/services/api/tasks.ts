import client from "./client";
import type { TaskDto } from "./types";

export const tasksApi = {
  getAll: (params?: { companyId?: string; myTasks?: boolean }) =>
    client.get<TaskDto[]>("/tasks", { params }),

  getById: (id: string) =>
    client.get<TaskDto>(`/tasks/${id}`),

  create: (data: { companyId: string; title: string; description?: string; labels?: string; deadline?: string; assignedToUserId?: string }) =>
  client.post<TaskDto>("/tasks", data),

  update: (id: string, data: Partial<{ title: string; description: string; labels: string; deadline: string; status: string; assignedToUserId: string | null }>) =>
    client.put<TaskDto>(`/tasks/${id}`, data),

  delete: (id: string) =>
    client.delete(`/tasks/${id}`),
};
