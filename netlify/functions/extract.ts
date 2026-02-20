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

CANONICAL KEY NAMES (use these so the review UI can read fields):
- document: use "docType" (not "type"), "documentDate" (not "issuanceDate"), "documentNumber".
- case: use "caseNumber" (not "fileNumber"); use "court" as an object with "name", "section", "registryAddress", "registryPhone", "registryHours" (not a string).
- parties: use "debtor" as an object with "name", "cui", "tradeRegisterNo", "address", "locality", "county", "administrator", etc.; use "practitioner" (not "appointedLiquidator") with "name", "fiscalId", "rfo", "address", "role".

Now output JSON with EXACTLY this schema:
{ ... }`;

/** Canonical keys (use these so the review UI can read fields): document.docType, document.documentDate, document.documentNumber; case.caseNumber; case.court as object with .name; parties.debtor as object; parties.practitioner (not appointedLiquidator). */

const str = (v: unknown) =>
  typeof v === "string" && v.trim() ? v : "Not found";

function dateObj(v: unknown): { text: string; iso: string | null } {
  if (v && typeof v === "object" && "text" in (v as object) && "iso" in (v as object)) {
    const d = v as { text?: string; iso?: string | null };
    return {
      text: typeof d.text === "string" ? d.text : "Not found",
      iso: typeof d.iso === "string" ? d.iso : null,
    };
  }
  if (typeof v === "string" && v.trim()) return { text: v, iso: null };
  return { text: "Not found", iso: null };
}

const defaultCourt = {
  name: "Not found",
  section: "Not found",
  registryAddress: "Not found",
  registryPhone: "Not found",
  registryHours: "Not found",
};

const defaultProcedure = {
  law: "Legea 85/2014",
  procedureType: "other",
  stage: "unknown",
  administrationRightLifted: null as boolean | null,
  legalBasisArticles: [] as string[],
};

const defaultImportantDates = {
  requestFiledDate: { text: "Not found", iso: null as string | null },
  openingDate: { text: "Not found", iso: null as string | null },
  nextHearingDateTime: { text: "Not found", iso: null as string | null },
};

const defaultPractitioner = {
  role: "Not found",
  name: "Not found",
  fiscalId: "Not found",
  rfo: "Not found",
  representative: "Not found",
  address: "Not found",
  email: "Not found",
  phone: "Not found",
  fax: "Not found",
  appointedDate: { text: "Not found", iso: null as string | null },
  confirmedDate: { text: "Not found", iso: null as string | null },
};

const defaultClaims = {
  tableType: "unknown",
  tableDate: { text: "Not found", iso: null as string | null },
  totalAdmittedRon: null as number | null,
  totalDeclaredRon: null as number | null,
  currency: "Not found",
  entries: [] as unknown[],
};

const defaultCreditorsMeeting = {
  meetingDate: { text: "Not found", iso: null as string | null },
  meetingTime: "Not found",
  location: "Not found",
  quorumPercent: null as number | null,
  agenda: [] as string[],
  decisions: {
    practitionerConfirmed: null as boolean | null,
    committeeFormed: null as boolean | null,
    committeeNotes: "Not found",
    feeApproved: {
      fixedFeeRon: null as number | null,
      vatIncluded: null as boolean | null,
      successFeePercent: null as number | null,
      paymentSource: "unknown" as const,
    },
  },
  votingSummary: "Not found",
};

const defaultReports = {
  art97: {
    issuedDate: { text: "Not found", iso: null as string | null },
    causesOfInsolvency: [] as string[],
    litigationFound: null as boolean | null,
    avoidanceReview: {
      reviewed: null as boolean | null,
      suspiciousTransactionsFound: null as boolean | null,
      actionsFiled: null as boolean | null,
      notes: "Not found",
    },
    liabilityAssessmentArt169: {
      reviewed: null as boolean | null,
      culpablePersonsIdentified: null as boolean | null,
      actionProposedOrFiled: null as boolean | null,
      notes: "Not found",
    },
    financials: {
      yearsCovered: [] as string[],
      totalAssetsRon: null as number | null,
      totalLiabilitiesRon: null as number | null,
      netEquityRon: null as number | null,
      cashRon: null as number | null,
      receivablesRon: null as number | null,
      notes: "Not found",
    },
  },
  finalArt167: {
    issuedDate: { text: "Not found", iso: null as string | null },
    assetsIdentified: null as boolean | null,
    saleableAssetsFound: null as boolean | null,
    sumsAvailableForDistributionRon: null as number | null,
    recoveryRatePercent: null as number | null,
    finalBalanceSheetDate: { text: "Not found", iso: null as string | null },
    closureProposed: null as boolean | null,
    closureLegalBasis: "Not found",
    deregistrationORCProposed: null as boolean | null,
    practitionerFeeRequestedFromUNPIR: null as boolean | null,
    notes: "Not found",
  },
};

const defaultComplianceFlags = {
  administrationRightLifted: null as boolean | null,
  individualActionsSuspended: null as boolean | null,
  publicationInBPIReferenced: null as boolean | null,
};

function normalizeExtraction(
  parsed: Record<string, unknown> | null,
  content: string,
): Record<string, unknown> {
  const doc = (parsed?.document as Record<string, unknown> | undefined) ?? {};
  const caseData = (parsed?.case as Record<string, unknown> | undefined) ?? {};
  const partiesData = (parsed?.parties as Record<string, unknown> | undefined) ?? {};

  // Document: map type → docType, issuanceDate → documentDate, case.fileNumber → documentNumber
  const document = {
    docType: doc.type ?? doc.docType ?? "other",
    language: str(doc.language) === "Not found" ? "ro" : str(doc.language),
    issuingEntity: str(doc.issuingEntity),
    documentNumber: str(doc.documentNumber) !== "Not found" ? str(doc.documentNumber) : str(caseData.fileNumber),
    documentDate: dateObj(doc.issuanceDate ?? doc.documentDate),
    sourceHints: str(doc.sourceHints),
  };

  // Case: caseNumber from fileNumber or caseNumber; court string → { name, ... }
  const courtRaw = caseData.court;
  const court =
    typeof courtRaw === "string"
      ? { ...defaultCourt, name: courtRaw.trim() || "Not found" }
      : courtRaw && typeof courtRaw === "object"
        ? {
            name: str((courtRaw as Record<string, unknown>).name) !== "Not found" ? str((courtRaw as Record<string, unknown>).name) : "Not found",
            section: str((courtRaw as Record<string, unknown>).section),
            registryAddress: str((courtRaw as Record<string, unknown>).registryAddress),
            registryPhone: str((courtRaw as Record<string, unknown>).registryPhone),
            registryHours: str((courtRaw as Record<string, unknown>).registryHours),
          }
        : defaultCourt;

  const procedureRaw = caseData.procedure;
  const procedure =
    procedureRaw && typeof procedureRaw === "object"
      ? {
          law: str((procedureRaw as Record<string, unknown>).law) !== "Not found" ? str((procedureRaw as Record<string, unknown>).law) : "Legea 85/2014",
          procedureType: (procedureRaw as Record<string, unknown>).procedureType ?? (procedureRaw as Record<string, unknown>).type ?? defaultProcedure.procedureType,
          stage: (procedureRaw as Record<string, unknown>).stage ?? defaultProcedure.stage,
          administrationRightLifted: (procedureRaw as Record<string, unknown>).administrationRightLifted ?? defaultProcedure.administrationRightLifted,
          legalBasisArticles: Array.isArray((procedureRaw as Record<string, unknown>).legalBasisArticles)
            ? (procedureRaw as Record<string, unknown>).legalBasisArticles as string[]
            : defaultProcedure.legalBasisArticles,
        }
      : defaultProcedure;

  const importantDatesRaw = caseData.importantDates;
  const importantDates =
    importantDatesRaw && typeof importantDatesRaw === "object"
      ? {
          requestFiledDate: dateObj((importantDatesRaw as Record<string, unknown>).requestFiledDate),
          openingDate: dateObj((importantDatesRaw as Record<string, unknown>).openingDate),
          nextHearingDateTime: dateObj((importantDatesRaw as Record<string, unknown>).nextHearingDateTime),
        }
      : defaultImportantDates;

  const caseResult = {
    caseNumber: str(caseData.caseNumber) !== "Not found" ? str(caseData.caseNumber) : str(caseData.fileNumber),
    court,
    judgeSyndic: str(caseData.judgeSyndic),
    procedure,
    importantDates,
  };

  // Parties – debtor: from parties.debtor (object) or case.debtor + parties.debtor (string); administrators → administrator
  const caseDebtor = caseData.debtor as Record<string, unknown> | undefined;
  const caseDebtorId = caseDebtor?.identifier as Record<string, unknown> | undefined;
  const partiesDebtorRaw = partiesData.debtor;
  const administratorsArr = partiesData.administrators;

  let debtor: Record<string, unknown>;
  if (partiesDebtorRaw && typeof partiesDebtorRaw === "object" && !Array.isArray(partiesDebtorRaw)) {
    const d = partiesDebtorRaw as Record<string, unknown>;
    debtor = {
      name: str(d.name),
      cui: str(d.cui),
      tradeRegisterNo: str(d.tradeRegisterNo),
      address: str(d.address),
      locality: str(d.locality),
      county: str(d.county),
      administrator: str(d.administrator) !== "Not found" ? str(d.administrator) : Array.isArray(administratorsArr) && administratorsArr.length > 0
        ? (administratorsArr as unknown[]).map(String).join(", ")
        : "Not found",
      associateOrShareholder: str(d.associateOrShareholder),
      caen: str(d.caen),
      incorporationYear: str(d.incorporationYear),
      shareCapitalRon: typeof d.shareCapitalRon === "number" ? d.shareCapitalRon : null,
    };
  } else {
    const nameFromParties = typeof partiesDebtorRaw === "string" ? partiesDebtorRaw.trim() || "Not found" : "Not found";
    debtor = {
      name: nameFromParties !== "Not found" ? nameFromParties : str(caseDebtor?.name),
      cui: str(caseDebtorId?.cui ?? caseDebtor?.cui),
      tradeRegisterNo: str(caseDebtorId?.registrationNumber ?? caseDebtor?.registrationNumber),
      address: str(caseDebtor?.address),
      locality: "Not found",
      county: "Not found",
      administrator: Array.isArray(administratorsArr) && administratorsArr.length > 0
        ? (administratorsArr as unknown[]).map(String).join(", ")
        : "Not found",
      associateOrShareholder: "Not found",
      caen: "Not found",
      incorporationYear: "Not found",
      shareCapitalRon: null,
    };
  }

  // Parties – practitioner: from parties.practitioner or parties.appointedLiquidator
  const practitionerRaw = partiesData.practitioner as Record<string, unknown> | undefined;
  const appointedLiquidator = partiesData.appointedLiquidator as Record<string, unknown> | undefined;
  const liquidatorId = appointedLiquidator?.identifier as Record<string, unknown> | undefined;

  let practitioner: Record<string, unknown>;
  if (practitionerRaw && typeof practitionerRaw === "object") {
    practitioner = {
      role: str(practitionerRaw.role),
      name: str(practitionerRaw.name),
      fiscalId: str(practitionerRaw.fiscalId),
      rfo: str(practitionerRaw.rfo),
      representative: str(practitionerRaw.representative),
      address: str(practitionerRaw.address),
      email: str(practitionerRaw.email),
      phone: str(practitionerRaw.phone),
      fax: str(practitionerRaw.fax),
      appointedDate: dateObj(practitionerRaw.appointedDate),
      confirmedDate: dateObj(practitionerRaw.confirmedDate),
    };
  } else if (appointedLiquidator && typeof appointedLiquidator === "object") {
    practitioner = {
      role: "lichidator_judiciar",
      name: str(appointedLiquidator.name),
      fiscalId: str(liquidatorId?.fiscalCode ?? appointedLiquidator.fiscalCode),
      rfo: str(liquidatorId?.registrationNumber ?? appointedLiquidator.registrationNumber),
      representative: "Not found",
      address: str(appointedLiquidator.headquarters ?? appointedLiquidator.address),
      email: "Not found",
      phone: "Not found",
      fax: "Not found",
      appointedDate: defaultPractitioner.appointedDate,
      confirmedDate: defaultPractitioner.confirmedDate,
    };
  } else {
    practitioner = { ...defaultPractitioner };
  }

  const parties = {
    debtor,
    practitioner,
    creditors: Array.isArray(partiesData.creditors) ? partiesData.creditors : [],
  };

  // Deadlines: pass through
  const deadlines = Array.isArray(parsed?.deadlines) ? parsed.deadlines : [];

  // Claims: if array or missing, use default object
  const claimsRaw = parsed?.claims;
  const claims =
    claimsRaw && typeof claimsRaw === "object" && !Array.isArray(claimsRaw)
      ? {
          tableType: (claimsRaw as Record<string, unknown>).tableType ?? defaultClaims.tableType,
          tableDate: dateObj((claimsRaw as Record<string, unknown>).tableDate),
          totalAdmittedRon: (claimsRaw as Record<string, unknown>).totalAdmittedRon ?? defaultClaims.totalAdmittedRon,
          totalDeclaredRon: (claimsRaw as Record<string, unknown>).totalDeclaredRon ?? defaultClaims.totalDeclaredRon,
          currency: str((claimsRaw as Record<string, unknown>).currency),
          entries: Array.isArray((claimsRaw as Record<string, unknown>).entries) ? (claimsRaw as Record<string, unknown>).entries : defaultClaims.entries,
        }
      : defaultClaims;

  // Creditors meeting: map single date → meetingDate
  const cmRaw = parsed?.creditorsMeeting as Record<string, unknown> | undefined;
  const creditorsMeeting =
    cmRaw && typeof cmRaw === "object"
      ? {
          ...defaultCreditorsMeeting,
          meetingDate: dateObj(cmRaw.date ?? cmRaw.meetingDate),
          meetingTime: str(cmRaw.meetingTime),
          location: str(cmRaw.location),
          quorumPercent: typeof cmRaw.quorumPercent === "number" ? cmRaw.quorumPercent : null,
          agenda: Array.isArray(cmRaw.agenda) ? cmRaw.agenda as string[] : [],
          decisions: cmRaw.decisions && typeof cmRaw.decisions === "object" ? cmRaw.decisions : defaultCreditorsMeeting.decisions,
          votingSummary: str(cmRaw.votingSummary),
        }
      : defaultCreditorsMeeting;

  // Reports: pass through with defaults for missing
  const reportsRaw = parsed?.reports;
  const reports =
    reportsRaw && typeof reportsRaw === "object"
      ? {
          art97: (reportsRaw as Record<string, unknown>).art97 && typeof (reportsRaw as Record<string, unknown>).art97 === "object"
            ? (reportsRaw as Record<string, unknown>).art97
            : defaultReports.art97,
          finalArt167: (reportsRaw as Record<string, unknown>).finalArt167 && typeof (reportsRaw as Record<string, unknown>).finalArt167 === "object"
            ? (reportsRaw as Record<string, unknown>).finalArt167
            : defaultReports.finalArt167,
        }
      : defaultReports;

  const complianceRaw = parsed?.complianceFlags as Record<string, unknown> | undefined;
  const complianceFlags =
    complianceRaw && typeof complianceRaw === "object"
      ? {
          administrationRightLifted: complianceRaw.administrationRightLifted ?? defaultComplianceFlags.administrationRightLifted,
          individualActionsSuspended: complianceRaw.individualActionsSuspended ?? defaultComplianceFlags.individualActionsSuspended,
          publicationInBPIReferenced: complianceRaw.publicationInBPIReferenced ?? defaultComplianceFlags.publicationInBPIReferenced,
        }
      : defaultComplianceFlags;

  return {
    document,
    case: caseResult,
    parties,
    deadlines,
    claims,
    creditorsMeeting,
    reports,
    complianceFlags,
    otherImportantInfo: str(parsed?.otherImportantInfo),
    rawJson: content,
  };
}

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

    const parsed = JSON.parse(content) as Record<string, unknown> | null;

    const result = normalizeExtraction(parsed, content);

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
