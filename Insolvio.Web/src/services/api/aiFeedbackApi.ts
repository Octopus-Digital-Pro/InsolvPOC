import client from "./client";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface AiCorrectionFeedbackDto {
  documentType: string;
  fieldName: string;
  aiSuggestedValue: string;
  userCorrectedValue: string;
  wasAccepted: boolean;
  aiConfidence: number | null;
  documentTextSnippet?: string;
  source: string;
}

export interface FieldAccuracyDto {
  fieldName: string;
  total: number;
  accepted: number;
  acceptanceRate: number;
}

export interface AiFeedbackStatsDto {
  totalCorrections: number;
  acceptedCount: number;
  correctedCount: number;
  acceptanceRate: number;
  fieldAccuracy: Record<string, FieldAccuracyDto>;
}

// ── API calls ─────────────────────────────────────────────────────────────────

export async function postCorrectionFeedback(corrections: AiCorrectionFeedbackDto[]) {
  const { data } = await client.post<{ recorded: number }>(
    "/ai-feedback/corrections",
    corrections
  );
  return data;
}

export async function getFeedbackStatistics(documentType?: string) {
  const params = documentType ? { documentType } : {};
  const { data } = await client.get<AiFeedbackStatsDto>(
    "/ai-feedback/statistics",
    { params }
  );
  return data;
}

// ── Diff Helper ───────────────────────────────────────────────────────────────

/**
 * Compare AI-suggested values against user-saved values and produce correction
 * DTOs for the feedback endpoint.
 */
export function computeCorrectionDiff(
  aiValues: Record<string, string | null | undefined>,
  userValues: Record<string, string | null | undefined>,
  documentType: string,
  source: string,
  confidences?: Record<string, number | null>,
  documentTextSnippet?: string
): AiCorrectionFeedbackDto[] {
  const diffs: AiCorrectionFeedbackDto[] = [];
  const allKeys = new Set([...Object.keys(aiValues), ...Object.keys(userValues)]);

  for (const key of allKeys) {
    const aiVal = aiValues[key] ?? "";
    const userVal = userValues[key] ?? "";
    if (aiVal === "" && userVal === "") continue; // skip fields with no data

    diffs.push({
      documentType,
      fieldName: key,
      aiSuggestedValue: aiVal,
      userCorrectedValue: userVal,
      wasAccepted: aiVal === userVal,
      aiConfidence: confidences?.[key] ?? null,
      documentTextSnippet,
      source,
    });
  }

  return diffs;
}
