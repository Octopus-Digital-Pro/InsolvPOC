import client from "./client";

export interface NotificationDto {
  id: string;
  title: string;
  message?: string;
  category: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
  relatedCaseId?: string;
  relatedEmailId?: string;
  relatedTaskId?: string;
  actionUrl?: string;
}

export const notificationsApi = {
  getRecent: (page = 1, pageSize = 20) =>
    client.get<NotificationDto[]>("/notifications", { params: { page, pageSize } }),

  getUnreadCount: () =>
    client.get<{ count: number }>("/notifications/unread-count"),

  markRead: (id: string) =>
    client.put(`/notifications/${id}/read`),

  markAllRead: () =>
    client.put("/notifications/read-all"),
};
