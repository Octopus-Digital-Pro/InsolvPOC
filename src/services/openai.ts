import OpenAI from "openai";

export type ContractExtractionResult = {
  // Parties
  beneficiary: string;
  beneficiaryAddress: string;
  beneficiaryIdentifiers: string;
  contractor: string;
  contractorAddress: string;
  contractorIdentifiers: string;
  subcontractors: string;

  // Contract core
  contractTitleOrSubject: string;
  contractNumberOrReference: string;
  procurementProcedure: string;
  cpvCodes: string;

  // Dates & period
  contractDate: string;
  effectiveDate: string;
  contractPeriod: string;

  // Signatures
  signatories: string;
  signingLocation: string;

  // Catch-all
  otherImportantClauses: string;
  rawJson: string;
};

const SYSTEM_PROMPT = `You are an expert public-sector contract analyst. You will be shown images of a contract or contract annexes between a city/government/public authority and a private contractor (including procurement/award documents that function as a contract).

Extract the following information from the document. If a field cannot be determined, set its value to "Not found". If multiple candidates exist, pick the most explicit one and include alternatives in the same string.

Return ONLY a valid JSON object with EXACTLY these keys:
beneficiary, beneficiaryAddress, beneficiaryIdentifiers,
contractor, contractorAddress, contractorIdentifiers,
subcontractors,
contractTitleOrSubject, contractNumberOrReference, procurementProcedure, cpvCodes,
contractDate, effectiveDate, contractPeriod,
signatories, signingLocation,
otherImportantClauses.

Extraction guidelines:
- "Beneficiary" means the public contracting authority (city/government/public institution) receiving the works/services.
- "Contractor" means the primary private entity obligated to deliver.
- If subcontractors are mentioned, list each with name + described role/scope.
- Dates: preserve the document's original format where possible; do not invent dates.
- Contract period: capture both textual duration (e.g., "12 months") and any explicit date range.
- otherImportantClauses: summarize critical clauses often present in public contracts (audit rights, confidentiality, data protection/GDPR, anti-corruption, change control, assignment, force majeure) ONLY if explicitly present.`;

function getClient(): OpenAI {
  const apiKey = import.meta.env.VITE_OPENAI_API_KEY;
  if (!apiKey) {
    throw new Error(
      "OpenAI API key not configured. Add VITE_OPENAI_API_KEY to your .env file.",
    );
  }
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
    max_tokens: 1200,
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
    effectiveDate: get("effectiveDate"),
    contractPeriod: get("contractPeriod"),

    signatories: get("signatories"),
    signingLocation: get("signingLocation"),

    otherImportantClauses: get("otherImportantClauses"),
    rawJson: content,
  };
}

// INSOLEVMENT CONTRACT EXTRACTION PROMPT - DO NOT DELETE!!!

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
