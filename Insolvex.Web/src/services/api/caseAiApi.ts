import client from "./client";

// ── DTOs ─────────────────────────────────────────────────────────────────────

export interface TenantAiConfigDto {
  id: string;
  aiEnabled: boolean;
  monthlyTokenLimit: number;
  currentMonthTokensUsed: number;
  summaryEnabled: boolean;
  chatEnabled: boolean;
  summaryActivityDays: number;
  notes: string | null;
  updatedAt: string | null;
}

export interface UpdateTenantAiConfigRequest {
  aiEnabled: boolean;
  monthlyTokenLimit: number;
  summaryEnabled: boolean;
  chatEnabled: boolean;
  summaryActivityDays: number;
  notes: string | null;
}

export interface AiChatMessageDto {
  id: string;
  role: "user" | "assistant";
  content: string;
  tokensUsed: number;
  model: string | null;
  createdAt: string;
  userId: string | null;
  userName: string | null;
}

export interface AiChatRequest {
  message: string;
  language: string;
}

export interface AiChatResponse {
  userMessage: AiChatMessageDto;
  assistantMessage: AiChatMessageDto;
  tokensUsed: number;
}

export interface AiSummaryDto {
  id: string;
  text: string;
  textByLanguageJson: string | null;
  generatedAt: string;
  model: string | null;
}

export interface AiSummaryRequest {
  language: string;
}

export interface AiEnabledStatus {
  aiEnabled: boolean;
  summaryEnabled: boolean;
  chatEnabled: boolean;
  usagePercent: number;
  atLimit: boolean;
}

// ── API clients ───────────────────────────────────────────────────────────────

export const tenantAiConfigApi = {
  getOwn: () => client.get<TenantAiConfigDto>("/settings/tenant-ai-config"),
  getForTenant: (tenantId: string) =>
    client.get<TenantAiConfigDto>(`/settings/tenant-ai-config/${tenantId}`),
  update: (tenantId: string, req: UpdateTenantAiConfigRequest) =>
    client.put<TenantAiConfigDto>(`/settings/tenant-ai-config/${tenantId}`, req),
};

export const caseAiApi = {
  checkEnabled: (caseId: string) =>
    client.get<AiEnabledStatus>(`/cases/${caseId}/ai/enabled`),

  generateSummary: (caseId: string, language: string) =>
    client.post<AiSummaryDto>(
      `/cases/${caseId}/ai/summary`,
      { language } as AiSummaryRequest
    ),

  getSummary: (caseId: string) =>
    client.get<AiSummaryDto | null>(`/cases/${caseId}/ai/summary`),

  getChatHistory: (caseId: string, take = 100) =>
    client.get<AiChatMessageDto[]>(`/cases/${caseId}/ai/chat`, { params: { take } }),

  sendMessage: (caseId: string, req: AiChatRequest) =>
    client.post<AiChatResponse>(`/cases/${caseId}/ai/chat`, req),

  clearHistory: (caseId: string) =>
    client.delete(`/cases/${caseId}/ai/chat`),
};
