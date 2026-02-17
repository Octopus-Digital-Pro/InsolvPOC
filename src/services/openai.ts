import OpenAI from "openai";
import type {ExtractionResult} from "../types";

/**
 * Consider creating a dedicated type instead of reusing ExtractionResult.
 * This example returns a richer object; adjust ../types accordingly.
 */
export type ContractExtractionResult = {
  // Parties
  beneficiary: string; // city/government entity (contracting authority)
  beneficiaryAddress: string;
  beneficiaryIdentifiers: string; // VAT/CUI/registration number, contract authority ID, etc.

  contractor: string; // main private contractor
  contractorAddress: string;
  contractorIdentifiers: string; // VAT/CUI/registration number, trade registry, etc.

  subcontractors: string; // list or "Not found" (include role/scope if stated)

  // Contract core
  contractTitleOrSubject: string;
  contractNumberOrReference: string; // contract no., tender no., project ref.
  procurementProcedure: string; // open tender, direct award, negotiated, etc. (if stated)
  cpvCodes: string; // if present (common in EU procurement)

  // Dates & period
  contractDate: string; // date stated as “Contract date” / “Date of contract”
  signatureDate: string; // date of signing (if separate)
  effectiveDate: string; // start/effective date (if stated)
  contractPeriod: string; // “12 months”, “01.03.2026–28.02.2027”, etc.
  startDate: string;
  endDate: string;
  extensionOptions: string; // renewal/extension clauses

  // Value & payment
  contractValue: string; // total value
  currency: string;
  vatIncludedOrExcluded: string; // “VAT included/excluded/Not found”
  paymentTerms: string; // schedule, invoicing, payment days

  // Scope & delivery
  scopeSummary: string; // concise scope
  deliverablesOrServices: string; // enumerated if possible
  milestonesDeadlines: string; // key deadlines, delivery dates, acceptance dates

  // Compliance & risk (often critical for public contracts)
  performanceGuarantee: string; // performance bond/guarantee amount & terms
  penaltiesOrLiquidatedDamages: string;
  terminationClausesSummary: string;
  governingLawAndJurisdiction: string;
  disputeResolution: string; // courts/arbitration/mediation

  // Signatures
  signatories: string; // who signed for each party + titles
  signingLocation: string;

  // Catch-all
  otherImportantClauses: string; // confidentiality, GDPR, audit rights, anti-corruption, etc.
  rawJson: string; // raw model response
};

const SYSTEM_PROMPT = `You are an expert public-sector contract analyst. You will be shown images of a contract or contract annexes between a city/government/public authority and a private contractor (including procurement/award documents that function as a contract).

Extract the following information from the document. If a field cannot be determined, set its value to "Not found". If multiple candidates exist, pick the most explicit one and include alternatives in the same string.

Return ONLY a valid JSON object with EXACTLY these keys:
beneficiary, beneficiaryAddress, beneficiaryIdentifiers,
contractor, contractorAddress, contractorIdentifiers,
subcontractors,
contractTitleOrSubject, contractNumberOrReference, procurementProcedure, cpvCodes,
contractDate, signatureDate, effectiveDate, contractPeriod, startDate, endDate, extensionOptions,
contractValue, currency, vatIncludedOrExcluded, paymentTerms,
scopeSummary, deliverablesOrServices, milestonesDeadlines,
performanceGuarantee, penaltiesOrLiquidatedDamages, terminationClausesSummary,
governingLawAndJurisdiction, disputeResolution,
signatories, signingLocation,
otherImportantClauses.

Extraction guidelines:
- "Beneficiary" means the public contracting authority (city/government/public institution) receiving the works/services.
- "Contractor" means the primary private entity obligated to deliver.
- If subcontractors are mentioned, list each with name + described role/scope.
- Dates: preserve the document’s original format where possible; do not invent dates.
- Contract period: capture both textual duration (e.g., "12 months") and any explicit start/end dates.
- Contract value: capture numeric value + any wording like "estimated" vs "fixed" if explicitly stated.
- Payment terms: include invoice timing, payment deadlines (e.g., "30 days"), advance payments, retention, etc.
- Milestones/deadlines: include delivery dates, acceptance dates, reporting dates, warranty periods if stated as date-bound obligations.
- otherImportantClauses: summarize critical clauses often present in public contracts (audit rights, confidentiality, data protection/GDPR, anti-corruption, change control, assignment, force majeure) ONLY if explicitly present.`;

function getClient(): OpenAI {
  const apiKey = import.meta.env.VITE_OPENAI_API_KEY;
  if (!apiKey) {
    throw new Error(
      "OpenAI API key not configured. Add VITE_OPENAI_API_KEY to your .env file.",
    );
  }

  // NOTE: exposing API keys in a browser is risky. Prefer calling your backend.
  return new OpenAI({apiKey, dangerouslyAllowBrowser: true});
}

export async function extractContractInfo(
  base64Images: string[],
): Promise<ContractExtractionResult> {
  const client = getClient();

  const imageMessages: OpenAI.Chat.Completions.ChatCompletionContentPart[] =
    base64Images.map((img) => ({
      type: "image_url" as const,
      image_url: {url: img, detail: "high" as const},
    }));

  const response = await client.chat.completions.create({
    model: "gpt-4o",
    response_format: {type: "json_object"},
    max_tokens: 1800,
    messages: [
      {role: "system", content: SYSTEM_PROMPT},
      {
        role: "user",
        content: [
          {
            type: "text",
            text: "Analyze this contract and extract the required contract fields as JSON.",
          },
          ...imageMessages,
        ],
      },
    ],
  });

  const content = response.choices[0]?.message?.content;
  if (!content) throw new Error("No response received from OpenAI");

  const parsed = JSON.parse(content);

  // Defensive defaults (ensures every key exists)
  const get = (k: keyof Omit<ContractExtractionResult, "rawJson">) =>
    (parsed?.[k] as string) || "Not found";

  return {
    beneficiary: get("beneficiary"),
    beneficiaryAddress: get("beneficiaryAddress"),
    beneficiaryIdentifiers: get("beneficiaryIdentifiers"),

    contractor: get("contractor"),
    contractorAddress: get("contractorAddress"),
    contractorIdentifiers: get("contractorIdentifiers"),

    subcontractors: get("subcontractors"),

    contractTitleOrSubject: get("contractTitleOrSubject"),
    contractNumberOrReference: get("contractNumberOrReference"),
    procurementProcedure: get("procurementProcedure"),
    cpvCodes: get("cpvCodes"),

    contractDate: get("contractDate"),
    signatureDate: get("signatureDate"),
    effectiveDate: get("effectiveDate"),
    contractPeriod: get("contractPeriod"),
    startDate: get("startDate"),
    endDate: get("endDate"),
    extensionOptions: get("extensionOptions"),

    contractValue: get("contractValue"),
    currency: get("currency"),
    vatIncludedOrExcluded: get("vatIncludedOrExcluded"),
    paymentTerms: get("paymentTerms"),

    scopeSummary: get("scopeSummary"),
    deliverablesOrServices: get("deliverablesOrServices"),
    milestonesDeadlines: get("milestonesDeadlines"),

    performanceGuarantee: get("performanceGuarantee"),
    penaltiesOrLiquidatedDamages: get("penaltiesOrLiquidatedDamages"),
    terminationClausesSummary: get("terminationClausesSummary"),
    governingLawAndJurisdiction: get("governingLawAndJurisdiction"),
    disputeResolution: get("disputeResolution"),

    signatories: get("signatories"),
    signingLocation: get("signingLocation"),

    otherImportantClauses: get("otherImportantClauses"),

    rawJson: content,
  };
}

// import OpenAI from 'openai';
// import type { ExtractionResult } from '../types';

// const SYSTEM_PROMPT = `You are an expert insolvency document analyst. You will be shown images of a legal/insolvency document (court filing, creditor notice, insolvency petition, etc.).

// Extract the following information from the document:

// 1. **companyName** – The name of the company that is subject to the insolvency proceedings (the debtor company).
// 2. **addressee** – The person or entity the document is addressed to (e.g. "To all known creditors", a specific person or firm).
// 3. **dateAndDeadlines** – All relevant dates and deadlines mentioned in the document. Include the document date, filing deadlines, hearing dates, claim submission deadlines, meeting dates, etc. Format each on its own line with a label, e.g. "Document date: 15 January 2025\\nClaim deadline: 28 February 2025".
// 4. **court** – The name and location of the court handling the insolvency case (e.g. "Amtsgericht München", "High Court of Justice, London").

// If a field cannot be determined from the document, set its value to "Not found".

// Respond ONLY with a valid JSON object using exactly these keys: companyName, addressee, dateAndDeadlines, court.`;

// function getClient(): OpenAI {
//   const apiKey = import.meta.env.VITE_OPENAI_API_KEY;
//   if (!apiKey) {
//     throw new Error(
//       'OpenAI API key not configured. Add VITE_OPENAI_API_KEY to your .env file.'
//     );
//   }
//   return new OpenAI({ apiKey, dangerouslyAllowBrowser: true });
// }

// export async function extractDocumentInfo(
//   base64Images: string[]
// ): Promise<ExtractionResult> {
//   const client = getClient();

//   const imageMessages: OpenAI.Chat.Completions.ChatCompletionContentPart[] =
//     base64Images.map((img) => ({
//       type: 'image_url' as const,
//       image_url: {
//         url: img,
//         detail: 'high' as const,
//       },
//     }));

//   const response = await client.chat.completions.create({
//     model: 'gpt-4o',
//     response_format: { type: 'json_object' },
//     max_tokens: 1500,
//     messages: [
//       { role: 'system', content: SYSTEM_PROMPT },
//       {
//         role: 'user',
//         content: [
//           {
//             type: 'text',
//             text: 'Please analyze this insolvency document and extract the required information.',
//           },
//           ...imageMessages,
//         ],
//       },
//     ],
//   });

//   const content = response.choices[0]?.message?.content;
//   if (!content) {
//     throw new Error('No response received from OpenAI');
//   }

//   const parsed = JSON.parse(content);

//   return {
//     companyName: parsed.companyName || 'Not found',
//     addressee: parsed.addressee || 'Not found',
//     dateAndDeadlines: parsed.dateAndDeadlines || 'Not found',
//     court: parsed.court || 'Not found',
//     rawText: content,
//   };
// }
