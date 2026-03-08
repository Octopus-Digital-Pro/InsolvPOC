import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface TrainingDocumentDto {
  id: string;
  documentType: string;
  originalFileName: string;
  reviewStatus: string | null;
  aiConfidence: number | null;
  aiModel: string | null;
  createdOn: string;
  lastModifiedOn: string | null;
}

export interface TrainingStatusDto {
  totalDocuments: number;
  approvedDocuments: number;
  pendingDocuments: number;
  canStartTraining: boolean;
  currentJobStatus: string | null;
  lastTrainingRun: string | null;
}

export interface TrainingDocumentsResponse {
  items: TrainingDocumentDto[];
  total: number;
  page: number;
  pageSize: number;
}

// ── API calls ─────────────────────────────────────────────────────────────────

export async function getTrainingDocuments(page = 1, pageSize = 20) {
  const { data } = await client.get<TrainingDocumentsResponse>("/training/documents", {
    params: { page, pageSize },
  });
  return data;
}

export async function uploadTrainingDocument(documentType: string, file: File) {
  const form = new FormData();
  form.append("documentType", documentType);
  form.append("file", file);
  const { data } = await client.post<TrainingDocumentDto>("/training/documents", form);
  return data;
}

export async function saveTrainingAnnotations(documentId: string, annotationsJson: string) {
  await client.put(`/training/documents/${encodeURIComponent(documentId)}/annotations`, {
    annotationsJson,
  });
}

export async function approveTrainingDocument(documentId: string) {
  await client.post(`/training/documents/${encodeURIComponent(documentId)}/approve`);
}

export async function getTrainingStatus() {
  const { data } = await client.get<TrainingStatusDto>("/training/status");
  return data;
}
