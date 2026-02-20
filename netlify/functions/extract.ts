import OpenAI from "openai";

/**
 * Insolvex – Insolvency Document Extraction (RO-only, numeric amounts where possible)
 *
 * Notes:
 * - Return ISO dates (YYYY-MM-DD) when unambiguous; otherwise keep original and set iso field null.
 * - Amounts: return numbers (RON) where possible; if not parseable, use null and explain in notes.
 * - Strings stay strings (no structured people objects).
 * - If a field cannot be determined, set it to "Not found" (for strings) or null (for numbers/booleans/dates).
 */
const SYSTEM_PROMPT = `You are an expert Romanian insolvency (Legea 85/2014) document analyst for the product "Insolvex".

You will be shown images of ONE insolvency-related document (court decision, notification, claims table, creditors meeting minutes, report art. 97, final report art. 167, etc.) for a Romanian case.

Your job:
1) Identify the document type.
2) Extract structured data into the EXACT JSON schema defined below.
3) Be precise: do not hallucinate. If unsure, use "Not found" or null.
4) Prefer the most explicit value. If multiple candidates exist, pick the most explicit and add alternatives in notes.

Hard rules:
- Return ONLY a valid JSON object with EXACTLY these top-level keys:
  document, case, parties, deadlines, claims, creditorsMeeting, reports, complianceFlags, otherImportantInfo
- Do NOT add extra top-level keys.
- For any string field that cannot be determined: use "Not found".
- For any number field that cannot be determined: use null.
- For any boolean field that cannot be determined: use null.
- Dates:
  - If you can confidently convert to ISO YYYY-MM-DD, do so in the relevant *iso* fields.
  - If not, keep original date text in *text* fields and set iso to null.
- Amounts:
  - Extract numeric values as numbers (RON) where possible (e.g., "45.255 lei" -> 45255).
  - If currency is not RON or unclear, still parse number but note currency in notes.
- Percentages:
  - Use numeric 0..100 (e.g., "5%" -> 5).

DOCUMENT TYPES:
Use one of:
- "court_opening_decision"
- "notification_opening"
- "report_art_97"
- "claims_table_preliminary"
- "claims_table_definitive"
- "creditors_meeting_minutes"
- "final_report_art_167"
- "other"

PROCEDURE TYPES:
Use one of:
- "faliment_simplificat"
- "faliment"
- "insolventa"
- "reorganizare"
- "other"

PROCEDURE STAGES:
Use one of:
- "request"
- "opened"
- "claims_window"
- "preliminary_table"
- "definitive_table"
- "liquidation"
- "final_report"
- "closure_requested"
- "closed"
- "unknown"

DEADLINE TYPES:
Use one of:
- "claims_submission"
- "claims_verification_preliminary_table"
- "definitive_table"
- "creditors_meeting"
- "appeal"
- "opposition"
- "next_hearing"
- "other"

CREDITOR TYPE (if inferable from table headings / legal rank):
Use one of:
- "bugetar"
- "salarial"
- "garantat"
- "chirografar"
- "altul"
- "unknown"

CLAIMS CATEGORY / RANK:
If explicit, capture the legal rank text, e.g.:
"Creanțe bugetare (art. 161 alin. (1) pct. 5)"
If not explicit: "Not found".

Now output JSON with EXACTLY this schema:
{ ... }`;

interface RequestBody {
  images: string[];
}

export default async function handler(req: Request) {
  if (req.method !== "POST") {
    return new Response(JSON.stringify({error: "Method not allowed"}), {
      status: 405,
      headers: {"Content-Type": "application/json"},
    });
  }

  const apiKey = process.env.OPENAI_API_KEY;
  if (!apiKey) {
    return new Response(
      JSON.stringify({error: "OPENAI_API_KEY not configured on server"}),
      {status: 500, headers: {"Content-Type": "application/json"}},
    );
  }

  try {
    const body = (await req.json()) as RequestBody;

    if (
      !body.images ||
      !Array.isArray(body.images) ||
      body.images.length === 0
    ) {
      return new Response(
        JSON.stringify({
          error: "Request must include a non-empty 'images' array",
        }),
        {status: 400, headers: {"Content-Type": "application/json"}},
      );
    }

    const client = new OpenAI({apiKey});

    const imageMessages: OpenAI.Chat.Completions.ChatCompletionContentPart[] =
      body.images.map((img: string) => ({
        type: "image_url" as const,
        image_url: {url: img, detail: "high" as const},
      }));

    const response = await client.chat.completions.create({
      model: "gpt-4o",
      response_format: {type: "json_object"},
      max_tokens: 2200,
      messages: [
        {role: "system", content: SYSTEM_PROMPT},
        {
          role: "user",
          content: [
            {
              type: "text",
              text: "Analyze this Romanian insolvency document and extract the required fields as JSON.",
            },
            ...imageMessages,
          ],
        },
      ],
    });

    const content = response.choices[0]?.message?.content;
    if (!content) {
      return new Response(
        JSON.stringify({error: "No response received from OpenAI"}),
        {status: 502, headers: {"Content-Type": "application/json"}},
      );
    }

    const parsed = JSON.parse(content);

    const str = (v: unknown) =>
      typeof v === "string" && v.trim() ? v : "Not found";

    const result = {
      document: parsed?.document ?? {
        docType: "other",
        language: "ro",
        issuingEntity: "Not found",
        documentNumber: "Not found",
        documentDate: {text: "Not found", iso: null},
        sourceHints: "Not found",
      },
      case: parsed?.case ?? {
        caseNumber: "Not found",
        court: {
          name: "Not found",
          section: "Not found",
          registryAddress: "Not found",
          registryPhone: "Not found",
          registryHours: "Not found",
        },
        judgeSyndic: "Not found",
        procedure: {
          law: "Legea 85/2014",
          procedureType: "other",
          stage: "unknown",
          administrationRightLifted: null,
          legalBasisArticles: [],
        },
        importantDates: {
          requestFiledDate: {text: "Not found", iso: null},
          openingDate: {text: "Not found", iso: null},
          nextHearingDateTime: {text: "Not found", iso: null},
        },
      },
      parties: parsed?.parties ?? {
        debtor: {
          name: "Not found",
          cui: "Not found",
          tradeRegisterNo: "Not found",
          address: "Not found",
          locality: "Not found",
          county: "Not found",
          administrator: "Not found",
          associateOrShareholder: "Not found",
          caen: "Not found",
          incorporationYear: "Not found",
          shareCapitalRon: null,
        },
        practitioner: {
          role: "Not found",
          name: "Not found",
          fiscalId: "Not found",
          rfo: "Not found",
          representative: "Not found",
          address: "Not found",
          email: "Not found",
          phone: "Not found",
          fax: "Not found",
          appointedDate: {text: "Not found", iso: null},
          confirmedDate: {text: "Not found", iso: null},
        },
        creditors: Array.isArray(parsed?.parties?.creditors)
          ? parsed.parties.creditors
          : [],
      },
      deadlines: Array.isArray(parsed?.deadlines) ? parsed.deadlines : [],
      claims: parsed?.claims ?? {
        tableType: "unknown",
        tableDate: {text: "Not found", iso: null},
        totalAdmittedRon: null,
        totalDeclaredRon: null,
        currency: "Not found",
        entries: [],
      },
      creditorsMeeting: parsed?.creditorsMeeting ?? {
        meetingDate: {text: "Not found", iso: null},
        meetingTime: "Not found",
        location: "Not found",
        quorumPercent: null,
        agenda: [],
        decisions: {
          practitionerConfirmed: null,
          committeeFormed: null,
          committeeNotes: "Not found",
          feeApproved: {
            fixedFeeRon: null,
            vatIncluded: null,
            successFeePercent: null,
            paymentSource: "unknown",
          },
        },
        votingSummary: "Not found",
      },
      reports: parsed?.reports ?? {
        art97: {
          issuedDate: {text: "Not found", iso: null},
          causesOfInsolvency: [],
          litigationFound: null,
          avoidanceReview: {
            reviewed: null,
            suspiciousTransactionsFound: null,
            actionsFiled: null,
            notes: "Not found",
          },
          liabilityAssessmentArt169: {
            reviewed: null,
            culpablePersonsIdentified: null,
            actionProposedOrFiled: null,
            notes: "Not found",
          },
          financials: {
            yearsCovered: [],
            totalAssetsRon: null,
            totalLiabilitiesRon: null,
            netEquityRon: null,
            cashRon: null,
            receivablesRon: null,
            notes: "Not found",
          },
        },
        finalArt167: {
          issuedDate: {text: "Not found", iso: null},
          assetsIdentified: null,
          saleableAssetsFound: null,
          sumsAvailableForDistributionRon: null,
          recoveryRatePercent: null,
          finalBalanceSheetDate: {text: "Not found", iso: null},
          closureProposed: null,
          closureLegalBasis: "Not found",
          deregistrationORCProposed: null,
          practitionerFeeRequestedFromUNPIR: null,
          notes: "Not found",
        },
      },
      complianceFlags: parsed?.complianceFlags ?? {
        administrationRightLifted: null,
        individualActionsSuspended: null,
        publicationInBPIReferenced: null,
      },
      otherImportantInfo: str(parsed?.otherImportantInfo),
      rawJson: content,
    };

    return new Response(JSON.stringify(result), {
      status: 200,
      headers: {"Content-Type": "application/json"},
    });
  } catch (err: unknown) {
    const message =
      err instanceof Error ? err.message : "Internal server error";
    return new Response(JSON.stringify({error: message}), {
      status: 500,
      headers: {"Content-Type": "application/json"},
    });
  }
}
