import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ValidationResultDto {
  canComplete: boolean;
  missingFields: string[];
  missingPartyRoles: string[];
  missingDocTypes: string[];
  missingTasks: string[];
  messages: string[];
}

export interface CaseWorkflowStageDto {
  id: string;
  caseId: string;
  stageDefinitionId: string;
  stageKey: string;
  name: string;
  description: string | null;
  sortOrder: number;
  status: "NotStarted" | "InProgress" | "Completed" | "Skipped";
  startedAt: string | null;
  completedAt: string | null;
  completedBy: string | null;
  validation: ValidationResultDto | null;
}

// ── API ───────────────────────────────────────────────────────────────────────

export const caseWorkflowApi = {
  /** Get all workflow stages for a case (auto-initialises on first call). */
  getStages: (caseId: string) =>
    client.get<CaseWorkflowStageDto[]>(`/cases/${caseId}/workflow`),

  /** Validate a single stage's requirements. */
  validate: (caseId: string, stageKey: string) =>
    client.get<ValidationResultDto>(`/cases/${caseId}/workflow/${stageKey}/validate`),

  /** Start a stage (gates on prior stages being complete/skipped). */
  start: (caseId: string, stageKey: string) =>
    client.post<CaseWorkflowStageDto>(`/cases/${caseId}/workflow/${stageKey}/start`),

  /** Complete a stage (validates requirements first). */
  complete: (caseId: string, stageKey: string) =>
    client.post<CaseWorkflowStageDto>(`/cases/${caseId}/workflow/${stageKey}/complete`),

  /** Skip a stage with optional reason. */
  skip: (caseId: string, stageKey: string, reason?: string) =>
    client.post<CaseWorkflowStageDto>(`/cases/${caseId}/workflow/${stageKey}/skip`, { reason }),

  /** Reopen a completed or skipped stage. */
  reopen: (caseId: string, stageKey: string) =>
    client.post<CaseWorkflowStageDto>(`/cases/${caseId}/workflow/${stageKey}/reopen`),
};
