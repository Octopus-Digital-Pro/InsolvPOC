import client from "./client";

export interface TemplateInfo {
  templateType: string;
  defaultFileName: string;
  diskExists: boolean;
  diskFileSizeBytes: number;
  tenantOverrideId: string | null;
  tenantOverrideFileName: string | null;
  tenantOverrideFileSizeBytes: number;
  tenantOverrideVersion: number;
  globalOverrideId: string | null;
  globalOverrideFileName: string | null;
  globalOverrideFileSizeBytes: number;
  globalOverrideVersion: number;
  /** "tenant" | "global-db" | "disk" | "missing" */
  effectiveSource: string;
}

export interface GeneratedDocResult {
  templateType: string;
  storageKey: string;
  fileName: string;
  fileSizeBytes: number;
  downloadUrl: string;
}

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

  // Mail merge / templates
  mailMerge: {
    getTemplates: () =>
      client.get<TemplateInfo[]>("/mailmerge/templates"),
    generate: (caseId: string, templateType: string) =>
      client.post<GeneratedDocResult>(`/mailmerge/generate/${caseId}`, { templateType }),
    generateAll: (caseId: string, detectedDocType?: string) =>
      client.post<GeneratedDocResult[]>(`/mailmerge/generate-all/${caseId}`, null, {
        params: detectedDocType ? { detectedDocType } : undefined,
      }),
    downloadUrl: (key: string) => `/api/mailmerge/download?key=${encodeURIComponent(key)}`,
  },
};
