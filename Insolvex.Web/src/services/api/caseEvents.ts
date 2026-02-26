import client from "./client";

export interface CaseEventDto {
  id: string;
  caseId: string;
  category: string;
  eventType: string;
  description: string;
  occurredAt: string;
  actorUserId: string | null;
  actorName: string | null;
  linkedEntityType: string | null;
  linkedEntityId: string | null;
  documentSummary: string | null;
  severity: string;
  metadataJson: string | null;
}

export interface CaseEventsPage {
  items: CaseEventDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const caseEventsApi = {
  get: (caseId: string, page = 1, pageSize = 50, category?: string) =>
    client.get<CaseEventsPage>(`/cases/${caseId}/events`, {
      params: { page, pageSize, category: category || undefined },
    }),
};
