import type { Company } from "../types";
import type { InsolvencyExtractionResult } from "./openai";

/** Normalize string for name matching: lowercase, single spaces, trimmed. */
export function normalizeForMatch(s: string): string {
  return (s || "").toLowerCase().replace(/\s+/g, " ").trim();
}

/**
 * Extract CUI/RO as digits only for reliable comparison.
 * Handles "RO12345678", "12345678", "CUI 12 34 56 78", etc.
 */
export function normalizeCuiRo(value: string): string {
  if (!value || value === "Not found") return "";
  const digits = value.replace(/\D/g, "");
  return digits;
}

/**
 * Get companies that match by name (substring either way) or by CUI in identifiers.
 * Used for medium-confidence matching and for suggestions in the UI.
 */
export function suggestCompanies(
  companies: Company[],
  name: string,
  identifiers: string,
): Company[] {
  if (!name || name === "Not found") return [];
  const n = normalizeForMatch(name);
  const idLower = (identifiers || "").toLowerCase();
  return companies.filter((c) => {
    const matchName =
      normalizeForMatch(c.name).includes(n) || n.includes(normalizeForMatch(c.name));
    const matchCui = c.cuiRo && idLower.includes(c.cuiRo.toLowerCase());
    return matchName || matchCui;
  });
}

/**
 * Returns a single company when there is exactly one high-confidence (debtor CUI) match
 * or exactly one medium-confidence (debtor name) suggestion; otherwise null.
 * Uses extraction.parties.debtor.name and extraction.parties.debtor.cui.
 */
export function getBestMatchingCompany(
  companies: Company[],
  extraction: InsolvencyExtractionResult,
): Company | null {
  if (companies.length === 0) return null;

  const debtorName = extraction.parties?.debtor?.name ?? "";
  const debtorCui = extraction.parties?.debtor?.cui ?? "";
  const debtorCuis = extractCuisFromIdentifiers(debtorCui);

  const companyByNormalizedCui = new Map<string, Company>();
  for (const c of companies) {
    const key = normalizeCuiRo(c.cuiRo);
    if (!key) continue;
    if (companyByNormalizedCui.has(key)) {
      companyByNormalizedCui.delete(key);
      continue;
    }
    companyByNormalizedCui.set(key, c);
  }

  const highConfidenceMatches = new Set<Company>();
  for (const cui of debtorCuis) {
    const company = companyByNormalizedCui.get(cui);
    if (company) highConfidenceMatches.add(company);
  }

  if (highConfidenceMatches.size === 1) {
    return [...highConfidenceMatches][0];
  }
  if (highConfidenceMatches.size > 1) {
    return null;
  }

  const debtorSuggestions = suggestCompanies(
    companies,
    debtorName,
    debtorCui,
  );
  if (debtorSuggestions.length === 1) return debtorSuggestions[0];

  return null;
}

/**
 * Extract possible CUI values (digit sequences, typically 4–15 chars) from an identifiers string.
 */
function extractCuisFromIdentifiers(identifiers: string): string[] {
  if (!identifiers || identifiers === "Not found") return [];
  const matches = identifiers.match(/\d{4,15}/g);
  if (!matches) return [];
  return [...new Set(matches)];
}
