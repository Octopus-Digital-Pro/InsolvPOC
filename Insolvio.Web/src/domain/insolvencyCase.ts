import type { InsolvencyDocument } from "../types";
import type {
  InsolvencyExtractionResult,
  InsolvencyDate,
} from "../services/openai";

/** Single deadline entry for aggregation (normalized date for sorting). */
export interface AggregatedDeadline {
  type: string;
  date: InsolvencyDate;
  time: string;
  legalBasis: string;
  notes: string;
  sourceDocId?: string;
}

/**
 * Collect and de-duplicate deadlines across documents.
 * Returns sorted by date (earliest first); null/unknown dates at end.
 */
export function aggregateDeadlines(
  documents: InsolvencyDocument[],
): AggregatedDeadline[] {
  const seen = new Set<string>();
  const result: AggregatedDeadline[] = [];
  for (const d of documents) {
    const raw = d.rawExtraction as InsolvencyExtractionResult | undefined;
    const list = raw?.deadlines ?? [];
    for (const entry of list) {
      const key = `${entry.type}-${entry.date?.iso ?? entry.date?.text ?? ""}-${entry.time}`;
      if (seen.has(key)) continue;
      seen.add(key);
      result.push({
        ...entry,
        sourceDocId: d.id,
      });
    }
  }
  result.sort((a, b) => {
    const isoA = a.date?.iso ?? "";
    const isoB = b.date?.iso ?? "";
    if (!isoA && !isoB) return 0;
    if (!isoA) return 1;
    if (!isoB) return -1;
    return isoA.localeCompare(isoB);
  });
  return result;
}

/** Get ISO string from InsolvencyDate for display/sorting. */
export function dateToIso(d: InsolvencyDate | undefined): string | null {
  if (!d) return null;
  if (typeof d === "string") return d;
  return d.iso ?? null;
}

/**
 * Get the next upcoming (earliest) hearing date across all case documents.
 * Considers both case.importantDates.nextHearingDateTime and deadlines with
 * type "next_hearing" (extraction may populate one or the other).
 * Returns ISO string or null if none found.
 */
export function getNextUpcomingHearingIso(
  documents: InsolvencyDocument[],
): string | null {
  const isos: string[] = [];
  for (const doc of documents) {
    const raw = doc.rawExtraction as InsolvencyExtractionResult | undefined;
    const fromImportantDates = raw?.case?.importantDates?.nextHearingDateTime;
    const iso1 = dateToIso(fromImportantDates);
    if (iso1) isos.push(iso1);
    const deadlineList = raw?.deadlines ?? [];
    for (const entry of deadlineList) {
      if (entry.type === "next_hearing") {
        const iso2 = dateToIso(entry.date);
        if (iso2) isos.push(iso2);
      }
    }
  }
  if (isos.length === 0) return null;
  isos.sort((a, b) => a.localeCompare(b));
  return isos[0];
}
