export type InsolvencyDate = {
  text: string;
  iso: string | null;
};

export type InsolvencyExtractionResult = {
  document: {
    docType:
      | "court_opening_decision"
      | "notification_opening"
      | "report_art_97"
      | "claims_table_preliminary"
      | "claims_table_definitive"
      | "creditors_meeting_minutes"
      | "final_report_art_167"
      | "other";
    language: string;
    issuingEntity: string;
    documentNumber: string;
    documentDate: InsolvencyDate;
    sourceHints: string;
  };
  case: {
    caseNumber: string;
    court: {
      name: string;
      section: string;
      registryAddress: string;
      registryPhone: string;
      registryHours: string;
    };
    judgeSyndic: string;
    procedure: {
      law: string;
      procedureType:
        | "faliment_simplificat"
        | "faliment"
        | "insolventa"
        | "reorganizare"
        | "other";
      stage:
        | "request"
        | "opened"
        | "claims_window"
        | "preliminary_table"
        | "definitive_table"
        | "liquidation"
        | "final_report"
        | "closure_requested"
        | "closed"
        | "unknown";
      administrationRightLifted: boolean | null;
      legalBasisArticles: string[];
    };
    importantDates: {
      requestFiledDate: InsolvencyDate;
      openingDate: InsolvencyDate;
      nextHearingDateTime: InsolvencyDate;
    };
  };
  parties: {
    debtor: {
      name: string;
      cui: string;
      tradeRegisterNo: string;
      address: string;
      locality: string;
      county: string;
      administrator: string;
      associateOrShareholder: string;
      caen: string;
      incorporationYear: string;
      shareCapitalRon: number | null;
    };
    practitioner: {
      role: "lichidator_judiciar" | "administrator_judiciar" | "Not found";
      name: string;
      fiscalId: string;
      rfo: string;
      representative: string;
      address: string;
      email: string;
      phone: string;
      fax: string;
      appointedDate: InsolvencyDate;
      confirmedDate: InsolvencyDate;
    };
    creditors: Array<{
      name: string;
      creditorType:
        | "bugetar"
        | "salarial"
        | "garantat"
        | "chirografar"
        | "altul"
        | "unknown";
      identifiers: string;
      address: string;
    }>;
  };
  deadlines: Array<{
    type:
      | "claims_submission"
      | "claims_verification_preliminary_table"
      | "definitive_table"
      | "creditors_meeting"
      | "appeal"
      | "opposition"
      | "next_hearing"
      | "other";
    date: InsolvencyDate;
    time: string;
    legalBasis: string;
    notes: string;
  }>;
  claims: {
    tableType: "preliminary" | "definitive" | "unknown";
    tableDate: InsolvencyDate;
    totalAdmittedRon: number | null;
    totalDeclaredRon: number | null;
    currency: string;
    entries: Array<{
      creditorName: string;
      creditorType:
        | "bugetar"
        | "salarial"
        | "garantat"
        | "chirografar"
        | "altul"
        | "unknown";
      declaredAmountRon: number | null;
      admittedAmountRon: number | null;
      percentOfMass: number | null;
      rankOrCategory: string;
      securedDetails: string;
      notes: string;
    }>;
  };
  creditorsMeeting: {
    meetingDate: InsolvencyDate;
    meetingTime: string;
    location: string;
    quorumPercent: number | null;
    agenda: string[];
    decisions: {
      practitionerConfirmed: boolean | null;
      committeeFormed: boolean | null;
      committeeNotes: string;
      feeApproved: {
        fixedFeeRon: number | null;
        vatIncluded: boolean | null;
        successFeePercent: number | null;
        paymentSource: "estate" | "UNPIR_fund" | "unknown";
      };
    };
    votingSummary: string;
  };
  reports: {
    art97: {
      issuedDate: InsolvencyDate;
      causesOfInsolvency: string[];
      litigationFound: boolean | null;
      avoidanceReview: {
        reviewed: boolean | null;
        suspiciousTransactionsFound: boolean | null;
        actionsFiled: boolean | null;
        notes: string;
      };
      liabilityAssessmentArt169: {
        reviewed: boolean | null;
        culpablePersonsIdentified: boolean | null;
        actionProposedOrFiled: boolean | null;
        notes: string;
      };
      financials: {
        yearsCovered: string[];
        totalAssetsRon: number | null;
        totalLiabilitiesRon: number | null;
        netEquityRon: number | null;
        cashRon: number | null;
        receivablesRon: number | null;
        notes: string;
      };
    };
    finalArt167: {
      issuedDate: InsolvencyDate;
      assetsIdentified: boolean | null;
      saleableAssetsFound: boolean | null;
      sumsAvailableForDistributionRon: number | null;
      recoveryRatePercent: number | null;
      finalBalanceSheetDate: InsolvencyDate;
      closureProposed: boolean | null;
      closureLegalBasis: string;
      deregistrationORCProposed: boolean | null;
      practitionerFeeRequestedFromUNPIR: boolean | null;
      notes: string;
    };
  };
  complianceFlags: {
    administrationRightLifted: boolean | null;
    individualActionsSuspended: boolean | null;
    publicationInBPIReferenced: boolean | null;
  };
  otherImportantInfo: string;
  rawJson: string;
};

export async function extractInsolvencyInfo(
  base64Images: string[],
): Promise<InsolvencyExtractionResult> {
  const response = await fetch("/.netlify/functions/extract", {
    method: "POST",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({images: base64Images}),
  });

  if (!response.ok) {
    const err = await response
      .json()
      .catch(() => ({error: response.statusText}));
    throw new Error(
      (err as {error?: string}).error || `Server error ${response.status}`,
    );
  }

  return (await response.json()) as InsolvencyExtractionResult;
}
