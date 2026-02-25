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

export const settingsApi = {
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
