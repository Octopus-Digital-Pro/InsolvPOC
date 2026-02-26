import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface WorkflowStageDto {
  id: string;
  tenantId: string | null;
  stageKey: string;
  name: string;
  description: string | null;
  sortOrder: number;
  applicableProcedureTypes: string | null;
  isActive: boolean;
  templateCount: number;
  createdOn: string;
  lastModifiedOn: string | null;
}

export interface WorkflowStageTemplateDto {
  id: string;
  documentTemplateId: string;
  templateName: string;
  templateType: string | null;
  isRequired: boolean;
  sortOrder: number;
  notes: string | null;
}

export interface WorkflowStageDetailDto extends Omit<WorkflowStageDto, "templateCount"> {
  requiredFieldsJson: string | null;
  requiredPartyRolesJson: string | null;
  requiredDocTypesJson: string | null;
  requiredTaskTemplatesJson: string | null;
  validationRulesJson: string | null;
  outputDocTypesJson: string | null;
  outputTasksJson: string | null;
  allowedTransitionsJson: string | null;
  templates: WorkflowStageTemplateDto[];
}

export interface UpsertWorkflowStageCommand {
  stageKey: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  applicableProcedureTypes?: string | null;
  requiredFieldsJson?: string | null;
  requiredPartyRolesJson?: string | null;
  requiredDocTypesJson?: string | null;
  requiredTaskTemplatesJson?: string | null;
  validationRulesJson?: string | null;
  outputDocTypesJson?: string | null;
  outputTasksJson?: string | null;
  allowedTransitionsJson?: string | null;
  isActive?: boolean;
  templates?: UpsertStageTemplateItem[] | null;
}

export interface UpsertStageTemplateItem {
  documentTemplateId: string;
  isRequired: boolean;
  sortOrder: number;
  notes?: string | null;
}

// ── API ───────────────────────────────────────────────────────────────────────

export const workflowStagesApi = {
  /** Resolved list: tenant override → global fallback. */
  getEffective: () =>
    client.get<WorkflowStageDto[]>("/workflow-stages"),

  /** Global stages only (admin). */
  getGlobal: () =>
    client.get<WorkflowStageDto[]>("/workflow-stages/global"),

  /** Detail with JSON configs + linked templates. */
  getById: (id: string) =>
    client.get<WorkflowStageDetailDto>(`/workflow-stages/${id}`),

  /** Create or update a global stage definition. */
  upsertGlobal: (cmd: UpsertWorkflowStageCommand) =>
    client.post<WorkflowStageDetailDto>("/workflow-stages/global", cmd),

  /** Create or update a tenant override. */
  upsertOverride: (cmd: UpsertWorkflowStageCommand) =>
    client.post<WorkflowStageDetailDto>("/workflow-stages/override", cmd),

  /** Remove tenant override (revert to global). */
  deleteOverride: (stageKey: string) =>
    client.delete(`/workflow-stages/override/${stageKey}`),

  /** Delete a global stage definition. */
  deleteGlobal: (stageKey: string) =>
    client.delete(`/workflow-stages/global/${stageKey}`),
};
