import client from "./client";

export const workflowApi = {
  // Stage timeline
  getTimeline: (caseId: string) =>
    client.get(`/workflow/${caseId}/timeline`),

  // Validation
  validate: (caseId: string) =>
 client.get(`/workflow/${caseId}/validate`),

  // Advance stage
  advance: (caseId: string) =>
 client.post(`/workflow/${caseId}/advance`),

  // Stage definitions
  getStageDefinitions: () =>
    client.get("/workflow/stages"),

  // Deadline settings
  getDeadlineSettings: (caseId?: string, tenantId?: string) =>
    client.get("/deadline-settings", { params: { caseId, tenantId } }),
  previewDeadlines: (noticeDate: string, tenantId?: string) =>
    client.get("/deadline-settings/preview", { params: { noticeDate, tenantId } }),

  // Creditor meeting
  createMeeting: (data: {
 caseId: string;
    meetingDate: string;
    location?: string;
    agenda?: string;
    durationHours?: number;
  }) => client.post("/creditor-meeting", data),
  getCaseCalendar: (caseId: string) =>
 client.get(`/creditor-meeting/calendar/${caseId}`),

  // Case summary
  generateSummary: (caseId: string, trigger?: string) =>
 client.post(`/case-summary/${caseId}/generate`, null, { params: { trigger } }),
  getLatestSummary: (caseId: string) =>
    client.get(`/case-summary/${caseId}/latest`),
  getSummaryHistory: (caseId: string, take?: number) =>
 client.get(`/case-summary/${caseId}/history`, { params: { take } }),
};
