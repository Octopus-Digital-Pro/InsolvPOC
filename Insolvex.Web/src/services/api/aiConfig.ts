import client from "./client";

export type AiProvider = "OpenAI" | "AzureOpenAI" | "Anthropic" | "Google" | "Custom";

export interface AiConfigDto {
  id: string;
  provider: AiProvider;
  hasApiKey: boolean;
  apiEndpoint: string | null;
  modelName: string | null;
  deploymentName: string | null;
  isEnabled: boolean;
  notes: string | null;
  updatedAt: string | null;
}

export interface UpdateAiConfigRequest {
  provider: AiProvider;
  /** null = unchanged, "" = clear, any value = encrypt + replace */
  apiKey: string | null;
  apiEndpoint: string | null;
  modelName: string | null;
  deploymentName: string | null;
  isEnabled: boolean;
  notes: string | null;
}

export const aiConfigApi = {
  get: () => client.get<AiConfigDto>("/settings/ai-config"),
  update: (data: UpdateAiConfigRequest) =>
    client.put<AiConfigDto>("/settings/ai-config", data),
};
