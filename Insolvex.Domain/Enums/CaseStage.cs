namespace Insolvex.Domain.Enums;

/// <summary>
/// Stage-based workflow aligned with InsolvencyAppRules.md.
/// Each stage has required tasks, documents, data completeness checks,
/// and validation gates to advance to the next stage.
/// </summary>
public enum CaseStage
{
    /// <summary>Stage 0: Upload notice, extract NoticeDate, create baseline tasks</summary>
    Intake = 0,

    /// <summary>Stage 1: Verify debtor, confirm procedure type, generate initial notices</summary>
    EligibilitySetup = 1,

    /// <summary>Stage 2: Mail-merge notices, email parties, log delivery proof</summary>
    FormalNotifications = 2,

    /// <summary>Stage 3: Collect claims, build creditor register, handle disputes</summary>
    CreditorClaims = 3,

    /// <summary>Stage 4: Collect financials, identify assets, create asset register</summary>
    AssetAssessment = 4,

    /// <summary>Stage 5: Convene creditor meeting, capture resolutions, store minutes</summary>
    CreditorMeeting = 5,

    /// <summary>Stage 6: Sell assets, distribute proceeds, record payments</summary>
    RealisationDistributions = 6,

    /// <summary>Stage 7: Periodic reports, statutory filings, compliance checks</summary>
    ReportingCompliance = 7,

    /// <summary>Stage 8: Final report, archive, close open tasks</summary>
    Closure = 8,

    // ?? Legacy values kept for backward compatibility ??

    /// <summary>Legacy: mapped to Intake</summary>
    [Obsolete("Use Intake")] Request = 100,
    /// <summary>Legacy: mapped to EligibilitySetup</summary>
    [Obsolete("Use EligibilitySetup")] Opened = 101,
    /// <summary>Legacy: mapped to CreditorClaims</summary>
    [Obsolete("Use CreditorClaims")] ClaimsWindow = 102,
    /// <summary>Legacy: mapped to CreditorClaims</summary>
    [Obsolete("Use CreditorClaims")] PreliminaryTable = 103,
    /// <summary>Legacy: mapped to CreditorClaims</summary>
    [Obsolete("Use CreditorClaims")] DefinitiveTable = 104,
    /// <summary>Legacy: mapped to RealisationDistributions</summary>
    [Obsolete("Use RealisationDistributions")] Liquidation = 105,
    /// <summary>Legacy: mapped to ReportingCompliance</summary>
    [Obsolete("Use ReportingCompliance")] FinalReport = 106,
    /// <summary>Legacy: mapped to Closure</summary>
    [Obsolete("Use Closure")] ClosureRequested = 107,
    /// <summary>Legacy: mapped to Closure</summary>
    [Obsolete("Use Closure")] Closed = 108,

    Unknown = 999,
}
