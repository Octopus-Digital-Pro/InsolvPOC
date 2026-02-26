import client from "./client";
import type { TaskDto } from "./types";

export interface CaseEmailDto {
  id: string;
  caseId: string;
  to: string;
  cc: string | null;
  bcc: string | null;
  subject: string;
  body: string;
  scheduledFor: string;
  sentAt: string | null;
  status: string;
  isHtml: boolean;
  attachmentsJson: string | null;
  relatedTaskId: string | null;
}

export interface CaseCalendarEventDto {
  id: string;
  caseId: string;
title: string;
  description: string | null;
  start: string;
  end: string | null;
  allDay: boolean;
  location: string | null;
eventType: string;
  icsUrl: string | null;
}

export interface BulkEmailRequest {
  subject: string;
  body: string;
  cc?: string;
  bcc?: string;
  isHtml?: boolean;
  scheduledFor?: string;
  attachmentsJson?: string;
  relatedTaskId?: string;
  roles?: string[];
}

export interface BulkEmailPreview {
  total: number;
  withEmail: number;
  withoutEmail: number;
  recipients: Array<{
    partyId: string;
    name: string | null;
    email: string | null;
    role: string;
    hasEmail: boolean;
  }>;
}

export const caseTasksApi = {
  getByCaseId: (caseId: string) =>
    client.get<TaskDto[]>(`/cases/${caseId}/tasks`),

  create: (caseId: string, data: { title: string; description?: string; category?: string; deadline?: string; assignedToUserId?: string; isCriticalDeadline?: boolean }) =>
    client.post<TaskDto>(`/cases/${caseId}/tasks`, data),
};

export const caseEmailsApi = {
  getByCaseId: (caseId: string) =>
    client.get<CaseEmailDto[]>(`/cases/${caseId}/emails`),

  schedule: (caseId: string, data: { to: string; subject: string; body: string; scheduledFor?: string; cc?: string; bcc?: string }) =>
    client.post<CaseEmailDto>(`/cases/${caseId}/emails`, data),

  bulkSend: (caseId: string, data: BulkEmailRequest) =>
    client.post(`/cases/${caseId}/bulk-email/creditor-cohort`, data),

  previewCohort: (caseId: string, roles?: string) =>
    client.get<BulkEmailPreview>(`/cases/${caseId}/bulk-email/creditor-cohort/preview`, { params: { roles } }),
};

export const caseCalendarApi = {
  getByCaseId: (caseId: string) =>
    client.get<CaseCalendarEventDto[]>(`/cases/${caseId}/calendar`),
};
