import client from "./client";
import type { TaskDto, TaskNoteDto } from "./types";

export const tasksApi = {
  getAll: (params?: { companyId?: string; myTasks?: boolean }) =>
    client.get<TaskDto[]>("/tasks", { params }),

  getById: (id: string) =>
    client.get<TaskDto>(`/tasks/${id}`),

  create: (data: { companyId: string; title: string; description?: string; labels?: string; deadline?: string; assignedToUserId?: string }) =>
  client.post<TaskDto>("/tasks", data),

  update: (id: string, data: Partial<{ title: string; description: string; labels: string; deadline: string; status: string; blockReason: string | null; assignedToUserId: string | null }>) =>
    client.put<TaskDto>(`/tasks/${id}`, data),

  delete: (id: string) =>
    client.delete(`/tasks/${id}`),

  // Notes
  getNotes: (taskId: string) =>
    client.get<TaskNoteDto[]>(`/tasks/${taskId}/notes`),

  addNote: (taskId: string, content: string) =>
    client.post<TaskNoteDto>(`/tasks/${taskId}/notes`, { content }),

  updateNote: (taskId: string, noteId: string, content: string) =>
    client.put<TaskNoteDto>(`/tasks/${taskId}/notes/${noteId}`, { content }),

  deleteNote: (taskId: string, noteId: string) =>
    client.delete(`/tasks/${taskId}/notes/${noteId}`),
};
