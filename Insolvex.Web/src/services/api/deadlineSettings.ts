import client from "./client";

export interface TenantDeadlineSettingsDto {
  id: string;
  tenantId: string;
  sendInitialNoticeWithinDays: number;
  claimDeadlineDaysFromNotice: number;
objectionDeadlineDaysFromNotice: number;
  meetingNoticeMinimumDays: number;
  reportEveryNDays: number;
  useBusinessDays: boolean;
  adjustToNextWorkingDay: boolean;
  reminderDaysBeforeDeadline: string;
  urgentQueueHoursBeforeDeadline: number;
  autoAssignBackupOnCriticalOverdue: boolean;
  emailFromName: string | null;
}

export interface CaseDeadlineOverrideDto {
  id: string;
  caseId: string;
  deadlineKey: string;
  originalValue: string | null;
  overrideValue: string;
  reason: string;
  isActive: boolean;
  overriddenAt: string;
  overriddenByUserId: string | null;
}

export interface DeadlinePreviewDto {
  claimDeadline: string;
  objectionDeadline: string;
  initialNoticeSendBy: string;
  firstReportDue: string;
}

export const deadlineSettingsApi = {
  getEffective: (params?: { caseId?: string; tenantId?: string }) =>
    client.get("/deadline-settings", { params }),

  preview: (noticeDate: string, tenantId?: string) =>
    client.get<DeadlinePreviewDto>("/deadline-settings/preview", { params: { noticeDate, tenantId } }),

  isWorkingDay: (date: string) =>
    client.get<{ date: string; isWorkingDay: boolean }>("/deadline-settings/is-working-day", { params: { date } }),

  // Tenant settings
  getTenantSettings: () =>
    client.get<TenantDeadlineSettingsDto>("/deadline-settings/tenant"),

  updateTenantSettings: (data: Partial<TenantDeadlineSettingsDto>) =>
    client.put<TenantDeadlineSettingsDto>("/deadline-settings/tenant", data),

  // Case overrides
  getCaseOverrides: (caseId: string) =>
    client.get<CaseDeadlineOverrideDto[]>(`/deadline-settings/case/${caseId}/overrides`),

  createCaseOverride: (caseId: string, data: { deadlineKey: string; overrideValue: string; reason: string }) =>
    client.post<CaseDeadlineOverrideDto>(`/deadline-settings/case/${caseId}/overrides`, data),

  deactivateOverride: (caseId: string, overrideId: string) =>
 client.delete(`/deadline-settings/case/${caseId}/overrides/${overrideId}`),
};
