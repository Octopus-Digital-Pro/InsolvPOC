import type { Company } from "../types";
import type { ContractExtractionResult } from "./openai";

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
 * Returns a single company when there is exactly one high-confidence (CUI) match
 * or exactly one medium-confidence (name/CUI suggestion) match; otherwise null.
 * Considers both beneficiary and contractor; if both sides match different companies, returns null.
 */
export function getBestMatchingCompany(
  companies: Company[],
  extraction: ContractExtractionResult,
): Company | null {
  if (companies.length === 0) return null;

  // High confidence: match by normalized CUI from identifiers
  const beneficiaryCuis = extractCuisFromIdentifiers(extraction.beneficiaryIdentifiers);
  const contractorCuis = extractCuisFromIdentifiers(extraction.contractorIdentifiers);
  const companyByNormalizedCui = new Map<string, Company>();
  for (const c of companies) {
    const key = normalizeCuiRo(c.cuiRo);
    if (!key) continue;
    if (companyByNormalizedCui.has(key)) {
      // Duplicate CUI in companies -> multiple high-confidence match, do not auto-pick
      companyByNormalizedCui.delete(key);
      continue;
    }
    companyByNormalizedCui.set(key, c);
  }

  const highConfidenceMatches = new Set<Company>();
  for (const cui of beneficiaryCuis) {
    const company = companyByNormalizedCui.get(cui);
    if (company) highConfidenceMatches.add(company);
  }
  for (const cui of contractorCuis) {
    const company = companyByNormalizedCui.get(cui);
    if (company) highConfidenceMatches.add(company);
  }

  if (highConfidenceMatches.size === 1) {
    return [...highConfidenceMatches][0];
  }
  if (highConfidenceMatches.size > 1) {
    return null;
  }

  // Medium confidence: suggestCompanies for beneficiary and contractor
  const beneficiarySuggestions = suggestCompanies(
    companies,
    extraction.beneficiary,
    extraction.beneficiaryIdentifiers,
  );
  const contractorSuggestions = suggestCompanies(
    companies,
    extraction.contractor,
    extraction.contractorIdentifiers,
  );

  const beneficiaryIds = new Set(beneficiarySuggestions.map((c) => c.id));
  const contractorIds = new Set(contractorSuggestions.map((c) => c.id));
  const intersection = companies.filter(
    (c) => beneficiaryIds.has(c.id) && contractorIds.has(c.id),
  );
  if (intersection.length === 1) return intersection[0];

  if (beneficiarySuggestions.length === 1 && contractorSuggestions.length === 0) {
    return beneficiarySuggestions[0];
  }
  if (contractorSuggestions.length === 1 && beneficiarySuggestions.length === 0) {
    return contractorSuggestions[0];
  }
  if (
    beneficiarySuggestions.length === 1 &&
    contractorSuggestions.length === 1 &&
    beneficiarySuggestions[0].id === contractorSuggestions[0].id
  ) {
    return beneficiarySuggestions[0];
  }

  return null;
}

/**
 * Extract possible CUI values (digit sequences, typically 4–15 chars) from an identifiers string.
 * Avoids very short sequences (e.g. "Room 12") and deduplicates.
 */
function extractCuisFromIdentifiers(identifiers: string): string[] {
  if (!identifiers || identifiers === "Not found") return [];
  const matches = identifiers.match(/\d{4,15}/g);
  if (!matches) return [];
  const normalized = [...new Set(matches)];
  return normalized;
}
