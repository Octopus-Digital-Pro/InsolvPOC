using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Per-phase required tasks, documents, and key fields aligned with Romanian insolvency law (Legea 85/2014).
/// Used to auto-generate tasks when a phase transitions to InProgress and to drive the
/// phase-detail UI (checklist, required templates, gate status).
/// </summary>
public static class PhaseTaskDefinitions
{
    public static readonly IReadOnlyDictionary<PhaseType, PhaseTaskConfig> Phases =
        new Dictionary<PhaseType, PhaseTaskConfig>
        {
            [PhaseType.OpeningRequest] = new()
            {
                Goal = "File opening request and obtain court registration (Cerere de deschidere a procedurii).",
                RequiredTasks = new[]
                {
                    "Prepare opening request document",
                    "File opening request at tribunal",
                    "Obtain tribunal case number and registration",
                    "Attach balance sheet and provisional creditor list",
                },
                RequiredDocTypes = new[] { "opening_request", "balance_sheet" },
                RequiredFields = new[] { "DebtorName", "DebtorCui", "CourtName" },
                AutoTaskCategories = new[] { "Filing", "Document" },
            },

            [PhaseType.ObservationPeriod] = new()
            {
                Goal = "Manage observation period: appoint administrator, notify parties, publish in BPI.",
                RequiredTasks = new[]
                {
                    "Notify debtor management of procedure opening",
                    "Confirm appointment of judicial administrator",
                    "Generate and send initial BPI notification",
                    "Create initial creditor list from debtor records",
                    "Establish claim filing deadline",
                },
                RequiredDocTypes = new[] { "appointment_document", "bpi_publication" },
                RequiredFields = new[] { "OpeningDate", "PractitionerName", "ClaimsDeadline" },
                AutoTaskCategories = new[] { "Document", "Email", "Compliance" },
            },

            [PhaseType.CreditorNotification] = new()
            {
                Goal = "Notify all creditors and publish procedure in BPI and Official Gazette.",
                RequiredTasks = new[]
                {
                    "Mail merge creditor notification letters (Templates-Ro)",
                    "Send notifications to all known creditors",
                    "Send notification to ANAF and fiscal authorities",
                    "Publish notice in BPI (Buletinul Procedurilor de Insolventa)",
                    "Record and archive delivery proofs",
                },
                RequiredDocTypes = new[] { "creditor_notification", "bpi_publication", "delivery_proof" },
                RequiredFields = new[] { "ClaimsDeadline", "BpiPublicationNo" },
                AutoTaskCategories = new[] { "Email", "Document", "Compliance" },
            },

            [PhaseType.ClaimsFiling] = new()
            {
                Goal = "Collect and register all creditor claims within the statutory deadline.",
                RequiredTasks = new[]
                {
                    "Open claims intake portal / email ingestion",
                    "Register each received claim document",
                    "Review and classify each claim (accepted / rejected / needs info)",
                    "Request missing evidence from creditors",
                    "Build and maintain creditor register",
                },
                RequiredDocTypes = new[] { "creditor_claim" },
                RequiredFields = new[] { "ClaimsDeadline" },
                AutoTaskCategories = new[] { "Review", "Document", "Email" },
            },

            [PhaseType.PreliminaryClaimsTable] = new()
            {
                Goal = "Compile, verify and publish the preliminary claims table (Tabelul preliminar de creante).",
                RequiredTasks = new[]
                {
                    "Compile preliminary creditor claims table",
                    "Verify claim amounts and statutory priorities",
                    "File preliminary table with the court",
                    "Publish preliminary table in BPI",
                    "Notify creditors of table availability",
                },
                RequiredDocTypes = new[] { "claims_table_preliminary", "bpi_publication" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Filing", "Email" },
            },

            [PhaseType.ClaimsContestations] = new()
            {
                Goal = "Process contestations to the preliminary claims table and update based on court rulings.",
                RequiredTasks = new[]
                {
                    "Register all received contestations",
                    "Schedule court hearings for contestations",
                    "Prepare responses to each contestation",
                    "Update claim table based on court decisions",
                    "Record all court decision references",
                },
                RequiredDocTypes = new[] { "contestation", "court_hearing_record" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Filing", "Review", "Document" },
            },

            [PhaseType.DefinitiveClaimsTable] = new()
            {
                Goal = "Finalize and obtain court approval for the definitive claims table (Tabelul definitiv).",
                RequiredTasks = new[]
                {
                    "Compile definitive creditor claims table",
                    "Submit definitive table to court for approval",
                    "Publish definitive table in BPI",
                    "Notify creditors of final table",
                    "Record court approval decision reference",
                },
                RequiredDocTypes = new[] { "claims_table_definitive", "bpi_publication" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Filing", "Email" },
            },

            [PhaseType.CausesReport] = new()
            {
                Goal = "Investigate and report causes of insolvency (Raport Art. 97 Legea 85/2014).",
                RequiredTasks = new[]
                {
                    "Investigate causes and circumstances of insolvency",
                    "Identify potentially liable persons (Art. 169)",
                    "Generate Art. 97 report document from template",
                    "Submit report to court and creditors",
                    "File with BPI if required",
                },
                RequiredDocTypes = new[] { "causes_report_art97" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Report", "Filing" },
            },

            [PhaseType.ReorganizationPlanProposal] = new()
            {
                Goal = "Draft and submit the reorganization plan for creditor and court approval.",
                RequiredTasks = new[]
                {
                    "Draft reorganization plan with debtor/administrator",
                    "Financial projections and feasibility analysis",
                    "Obtain debtor approval of the plan",
                    "Submit reorganization plan to court",
                    "Notify creditors of plan availability",
                },
                RequiredDocTypes = new[] { "reorganization_plan" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Filing", "Review" },
            },

            [PhaseType.ReorganizationPlanVoting] = new()
            {
                Goal = "Conduct creditor vote on the reorganization plan.",
                RequiredTasks = new[]
                {
                    "Set creditor voting deadline",
                    "Send voting packs to all creditors",
                    "Collect and register votes",
                    "Count votes per creditor class",
                    "Record voting results with court",
                },
                RequiredDocTypes = new[] { "voting_register", "vote_results" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Meeting", "Document", "Email" },
            },

            [PhaseType.ReorganizationPlanConfirmation] = new()
            {
                Goal = "Obtain court confirmation of the reorganization plan.",
                RequiredTasks = new[]
                {
                    "Submit vote results to court",
                    "Attend court confirmation hearing",
                    "Obtain court confirmation decision",
                    "Publish confirmation in BPI",
                    "Notify all parties of confirmation",
                },
                RequiredDocTypes = new[] { "court_confirmation_decision" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Filing", "Document", "Email" },
            },

            [PhaseType.ReorganizationExecution] = new()
            {
                Goal = "Execute and monitor the reorganization plan implementation.",
                RequiredTasks = new[]
                {
                    "Implement plan activities per schedule",
                    "Monitor debtor compliance with plan",
                    "Generate periodic progress reports",
                    "Report quarterly to creditors and court",
                    "Handle any plan modification requests",
                },
                RequiredDocTypes = new[] { "progress_report", "compliance_report" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Report", "Compliance", "Review" },
            },

            [PhaseType.AssetLiquidation] = new()
            {
                Goal = "Liquidate debtor assets through auction or private sale (Lichidarea activelor).",
                RequiredTasks = new[]
                {
                    "Prepare and publish asset inventory",
                    "Obtain independent asset valuations",
                    "Organize auction or private sale process",
                    "Get court/creditor approval for sale strategy",
                    "Record sale proceeds and distribute",
                },
                RequiredDocTypes = new[] { "asset_valuation", "sale_agreement" },
                RequiredFields = new[] { "EstimatedAssetValueRon", "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Payment", "Filing" },
            },

            [PhaseType.CreditorDistribution] = new()
            {
                Goal = "Distribute recovered proceeds to creditors per statutory priority waterfall.",
                RequiredTasks = new[]
                {
                    "Create distribution plan per creditor class",
                    "Submit distribution plan to court for approval",
                    "Generate distribution statements (Templates-Ro)",
                    "Process payments to creditors",
                    "Record all payments with receipts",
                },
                RequiredDocTypes = new[] { "distribution_plan", "payment_receipts" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Payment", "Document", "Email" },
            },

            [PhaseType.FinalReport] = new()
            {
                Goal = "Generate the final case report and prepare for closure (Raportul final).",
                RequiredTasks = new[]
                {
                    "Compile final account statement",
                    "Generate final report (Art. 167 Legea 85/2014)",
                    "Submit final report to court",
                    "Send final report to creditors",
                    "Obtain court approval for closure",
                },
                RequiredDocTypes = new[] { "final_report" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Report", "Filing" },
            },

            [PhaseType.ProcedureClosure] = new()
            {
                Goal = "Close the insolvency procedure and archive all records (Inchiderea procedurii).",
                RequiredTasks = new[]
                {
                    "Obtain closure court decision",
                    "Generate closure notice (Templates-Ro)",
                    "Publish closure decision in BPI",
                    "Send closure notices to all parties",
                    "Archive all case documents",
                    "Complete post-case QA checklist",
                },
                RequiredDocTypes = new[] { "closure_decision", "closure_notice" },
                RequiredFields = new[] { "CourtDecisionRef" },
                AutoTaskCategories = new[] { "Document", "Email", "Compliance" },
            },
        };

    /// <summary>Get the phase config or null if not defined.</summary>
    public static PhaseTaskConfig? GetPhase(PhaseType phase) =>
        Phases.TryGetValue(phase, out var def) ? def : null;

    /// <summary>Resolve task category from title and phase auto-categories.</summary>
    public static string ResolveCategory(string taskTitle, string[] autoCategories)
    {
        var lower = taskTitle.ToLowerInvariant();
        if (lower.Contains("email") || lower.Contains("send") || lower.Contains("notification") || lower.Contains("notify")) return "Email";
        if (lower.Contains("generate") || lower.Contains("document") || lower.Contains("template") || lower.Contains("compile") || lower.Contains("report")) return "Document";
        if (lower.Contains("meeting") || lower.Contains("vote") || lower.Contains("attendance")) return "Meeting";
        if (lower.Contains("review") || lower.Contains("verify") || lower.Contains("check") || lower.Contains("classify")) return "Review";
        if (lower.Contains("file") || lower.Contains("filing") || lower.Contains("submit") || lower.Contains("publish")) return "Filing";
        if (lower.Contains("payment") || lower.Contains("distribut") || lower.Contains("proceeds")) return "Payment";
        if (lower.Contains("compliance") || lower.Contains("archive") || lower.Contains("qa")) return "Compliance";
        return autoCategories.FirstOrDefault() ?? "Review";
    }
}

/// <summary>Task and document requirements for a single procedural phase.</summary>
public class PhaseTaskConfig
{
    /// <summary>Short description of the phase goal.</summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>Titles of tasks that should be auto-generated when this phase starts.</summary>
    public string[] RequiredTasks { get; init; } = Array.Empty<string>();

    /// <summary>Document types that must be present before the phase is considered complete.</summary>
    public string[] RequiredDocTypes { get; init; } = Array.Empty<string>();

    /// <summary>Case/party field names that must be populated for this phase.</summary>
    public string[] RequiredFields { get; init; } = Array.Empty<string>();

    /// <summary>Task categories to use when resolving category from task title.</summary>
    public string[] AutoTaskCategories { get; init; } = Array.Empty<string>();
}
