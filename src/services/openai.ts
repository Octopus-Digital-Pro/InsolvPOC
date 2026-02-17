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

export async function extractContractInfo(
  base64Images: string[],
): Promise<ContractExtractionResult> {
  const response = await fetch('/.netlify/functions/extract', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ images: base64Images }),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error((err as { error?: string }).error || `Server error ${response.status}`);
  }

  return (await response.json()) as ContractExtractionResult;
}
