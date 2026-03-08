import client from "./client";
import type { TemplateInfo } from "./workflow";

export interface TemplateUploadResult {
  id: string;
  templateType: string;
  fileName: string;
  isGlobal: boolean;
  version: number;
  message: string;
}

export interface ClientErrorLogRequest {
  message: string;
  stackTrace?: string;
  source?: string;
  requestPath?: string;
  userAgent?: string;
  additionalContext?: string;
}

export interface SystemConfigItem {
  key: string;
  value: string;
  description: string | null;
  group: string | null;
}

export interface SystemConfigUpdateItem {
  key: string;
  value: string;
  description?: string;
  group?: string;
}

export const settingsApi = {
  emailPreferences: {
    get: () =>
      client.get<SystemConfigItem[]>("/settings/email-preferences"),
    update: (items: SystemConfigUpdateItem[]) =>
      client.put("/settings/email-preferences", { items }),
  },
  errors: {
    logClient: (data: ClientErrorLogRequest) =>
      client.post("/settings/errors/client", data),
  },
  templates: {
    getAll: () =>
      client.get<TemplateInfo[]>("/settings/templates"),

    upload: (
      file: File,
      templateType: string,
      opts?: { name?: string; description?: string; global?: boolean }
    ) => {
      const fd = new FormData();
      fd.append("file", file);
      fd.append("templateType", templateType);
      if (opts?.name) fd.append("name", opts.name);
      if (opts?.description) fd.append("description", opts.description);
      if (opts?.global) fd.append("global", "true");
      return client.post<TemplateUploadResult>("/settings/templates/upload", fd, {
        headers: { "Content-Type": undefined },
      });
    },

    delete: (id: string) =>
      client.delete(`/settings/templates/${id}`),

    downloadUrl: (id: string) => `/api/settings/templates/${id}/download`,
  },
};
