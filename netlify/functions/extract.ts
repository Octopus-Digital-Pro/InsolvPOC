import OpenAI from "openai";

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

interface RequestBody {
  images: string[];
}

export default async function handler(req: Request) {
  // Only allow POST
  if (req.method !== "POST") {
    return new Response(JSON.stringify({ error: "Method not allowed" }), {
      status: 405,
      headers: { "Content-Type": "application/json" },
    });
  }

  const apiKey = process.env.OPENAI_API_KEY;
  if (!apiKey) {
    return new Response(
      JSON.stringify({ error: "OPENAI_API_KEY not configured on server" }),
      { status: 500, headers: { "Content-Type": "application/json" } }
    );
  }

  try {
    const body = (await req.json()) as RequestBody;

    if (!body.images || !Array.isArray(body.images) || body.images.length === 0) {
      return new Response(
        JSON.stringify({ error: "Request must include a non-empty 'images' array" }),
        { status: 400, headers: { "Content-Type": "application/json" } }
      );
    }

    const client = new OpenAI({ apiKey });

    const imageMessages: OpenAI.Chat.Completions.ChatCompletionContentPart[] =
      body.images.map((img: string) => ({
        type: "image_url" as const,
        image_url: { url: img, detail: "high" as const },
      }));

    const response = await client.chat.completions.create({
      model: "gpt-4o",
      response_format: { type: "json_object" },
      max_tokens: 1200,
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
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
    if (!content) {
      return new Response(
        JSON.stringify({ error: "No response received from OpenAI" }),
        { status: 502, headers: { "Content-Type": "application/json" } }
      );
    }

    const parsed = JSON.parse(content);

    const get = (k: string) => (parsed?.[k] as string) || "Not found";

    const result = {
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

    return new Response(JSON.stringify(result), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "Internal server error";
    return new Response(JSON.stringify({ error: message }), {
      status: 500,
      headers: { "Content-Type": "application/json" },
    });
  }
}
