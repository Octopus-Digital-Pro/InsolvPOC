using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Static workflow configuration aligned with InsolvencyAppRules.md.
/// Defines per-stage: required tasks, required documents, required data,
/// allowed transitions, and validation gate rules.
/// </summary>
public static class WorkflowDefinition
{
    public static readonly IReadOnlyDictionary<CaseStage, StageDefinition> Stages =
        new Dictionary<CaseStage, StageDefinition>
        {
            [CaseStage.Intake] = new()
            {
                Stage = CaseStage.Intake,
                Order = 0,
                Name = "Intake & Case Creation",
                Goal = "Create the case from the notice document and schedule baseline tasks.",
                NextStage = CaseStage.EligibilitySetup,
                RequiredTasks = new[]
     {
        "Upload original notice document",
 "Extract NoticeDate from notice",
   "Extract parties (debtor, court, practitioner)",
  "Generate baseline deadlines",
                "Assign case owner",
         },
                RequiredDocTypes = new[] { "original_notice" },
                ValidationRules = new[]
                {
        new ValidationRule("NoticeDate extracted and confirmed", ctx => ctx.Case.OpeningDate.HasValue),
  new ValidationRule("Case type chosen", ctx => ctx.Case.ProcedureType != ProcedureType.Other),
             new ValidationRule("Debtor party exists with identifier", ctx =>
      !string.IsNullOrWhiteSpace(ctx.Case.DebtorName) && !string.IsNullOrWhiteSpace(ctx.Case.DebtorCui)),
    new ValidationRule("Case owner assigned", ctx => ctx.Case.AssignedToUserId.HasValue),
   new ValidationRule("Baseline deadlines computed", ctx =>
  ctx.Case.ClaimsDeadline.HasValue || !string.IsNullOrWhiteSpace(ctx.Case.KeyDeadlinesJson)),
      },
                AutoTaskCategories = new[] { "Document", "Review", "Compliance" },
                TemplateTypes = Array.Empty<DocumentTemplateType>(),
            },

            [CaseStage.EligibilitySetup] = new()
            {
                Stage = CaseStage.EligibilitySetup,
                Order = 1,
                Name = "Eligibility & Setup",
                Goal = "Confirm basics, set scope, initialise statutory communications.",
                NextStage = CaseStage.FormalNotifications,
                RequiredTasks = new[]
   {
   "Verify debtor identity & registry details",
         "Confirm insolvency type",
           "Configure case-specific deadlines",
      "Create case-specific party list",
       "Generate initial notices (Templates-Ro)",
      },
                RequiredDocTypes = new[] { "debtor_registration", "appointment_document" },
                ValidationRules = new[]
            {
new ValidationRule("Debtor + Court + Practitioner parties exist", ctx => ctx.PartyRolesPresent("Debtor", "Court", "InsolvencyPractitioner")),
         new ValidationRule("Initial notice generated or scheduled", ctx => ctx.HasTemplateGenerated(DocumentTemplateType.CreditorNotificationBpi)),
   new ValidationRule("At least one delivery method per key party", ctx => ctx.KeyPartiesHaveDeliveryMethod()),
          },
                AutoTaskCategories = new[] { "Document", "Email", "Filing" },
                TemplateTypes = new[] { DocumentTemplateType.CourtOpeningDecision, DocumentTemplateType.CreditorNotificationBpi },
            },

            [CaseStage.FormalNotifications] = new()
            {
                Stage = CaseStage.FormalNotifications,
                Order = 2,
                Name = "Formal Notifications & Publication",
                Goal = "All required parties notified, communications tracked, proof retained.",
                NextStage = CaseStage.CreditorClaims,
                RequiredTasks = new[]
          {
      "Mail merge and generate required notices",
  "Email notices to parties",
          "Log delivery proof",
             "Create/verify calendar dates",
                },
                RequiredDocTypes = new[] { "proof_of_delivery", "notice_receipt" },
                ValidationRules = new[]
      {
   new ValidationRule("All required notices sent", ctx => ctx.AllRequiredNoticesSent()),
         new ValidationRule("No failed deliveries unresolved", ctx => ctx.NoUnresolvedDeliveryFailures()),
   new ValidationRule("Claim submission window dates stored", ctx => ctx.Case.ClaimsDeadline.HasValue),
        },
                AutoTaskCategories = new[] { "Email", "Document", "Compliance" },
                TemplateTypes = new[] { DocumentTemplateType.CreditorNotificationBpi },
            },

            [CaseStage.CreditorClaims] = new()
            {
                Stage = CaseStage.CreditorClaims,
                Order = 3,
                Name = "Creditor Claims Collection & Register",
                Goal = "Gather claims, validate, create the creditor register, prepare disputes.",
                NextStage = CaseStage.AssetAssessment,
                RequiredTasks = new[]
           {
            "Open claims intake",
   "Review received claims",
      "Build provisional creditor register",
        "Send missing evidence requests",
             },
                RequiredDocTypes = new[] { "creditor_claim", "claims_table_preliminary" },
                ValidationRules = new[]
       {
 new ValidationRule("Claim deadline reached or sufficient claims", ctx =>
      ctx.Case.ClaimsDeadline.HasValue && ctx.Case.ClaimsDeadline.Value <= DateTime.UtcNow),
          new ValidationRule("All claims reviewed", ctx => ctx.AllClaimsReviewed()),
 new ValidationRule("Creditor register generated", ctx => ctx.HasDocType("claims_table_preliminary")),
 },
                AutoTaskCategories = new[] { "Document", "Review", "Email" },
                TemplateTypes = new[] { DocumentTemplateType.PreliminaryClaimsTable },
            },

            [CaseStage.AssetAssessment] = new()
            {
                Stage = CaseStage.AssetAssessment,
                Order = 4,
                Name = "Asset & Liability Assessment",
                Goal = "Understand estate, recoveries, contracts, litigation possibilities.",
                NextStage = CaseStage.CreditorMeeting,
                RequiredTasks = new[]
        {
             "Collect debtor financials",
     "Identify assets and encumbrances",
          "Create asset list with valuations",
     "Document recovery strategy",
    },
                RequiredDocTypes = new[] { "financial_statement", "asset_valuation" },
                ValidationRules = new[]
            {
       new ValidationRule("Asset register created", ctx => ctx.Case.EstimatedAssetValueRon.HasValue),
             new ValidationRule("Recovery strategy documented", ctx => ctx.HasDocType("recovery_strategy") || ctx.HasDocType("internal_memo")),
     },
                AutoTaskCategories = new[] { "Document", "Review", "Payment" },
                TemplateTypes = new[] { DocumentTemplateType.ReportArt97 },
            },

            [CaseStage.CreditorMeeting] = new()
            {
                Stage = CaseStage.CreditorMeeting,
                Order = 5,
                Name = "Creditor Meeting",
                Goal = "Convene meeting, publish agenda, capture resolutions, store minutes.",
                NextStage = CaseStage.RealisationDistributions,
                RequiredTasks = new[]
  {
                "Generate meeting notice pack",
     "Send meeting invites/notices",
          "Prepare voting register",
              "Record attendance and votes",
   "Upload minutes and resolutions",
         },
                RequiredDocTypes = new[] { "meeting_notice", "meeting_agenda", "meeting_minutes" },
                ValidationRules = new[]
    {
        new ValidationRule("Meeting notices sent within minimum window", ctx => ctx.MeetingNoticesSentOnTime()),
          new ValidationRule("Minutes uploaded and resolutions recorded", ctx => ctx.HasDocType("meeting_minutes")),
             },
                AutoTaskCategories = new[] { "Meeting", "Document", "Email" },
                TemplateTypes = new[] { DocumentTemplateType.CreditorsMeetingMinutes },
            },

            [CaseStage.RealisationDistributions] = new()
            {
                Stage = CaseStage.RealisationDistributions,
                Order = 6,
                Name = "Realisation & Distributions",
                Goal = "Execute recovery plan, sell assets, distribute proceeds.",
                NextStage = CaseStage.ReportingCompliance,
                RequiredTasks = new[]
           {
 "Create sale processes",
       "Distribution schedule creation",
     "Generate distribution notices",
     "Record payments",
     },
                RequiredDocTypes = new[] { "sale_agreement", "distribution_statement" },
                ValidationRules = new[]
           {
   new ValidationRule("Distribution ledger balances", ctx => true), // stub
    new ValidationRule("Required approvals recorded", ctx => true), // stub
            },
                AutoTaskCategories = new[] { "Payment", "Document", "Email" },
                TemplateTypes = new[] { DocumentTemplateType.DefinitiveClaimsTable },
            },

            [CaseStage.ReportingCompliance] = new()
            {
                Stage = CaseStage.ReportingCompliance,
                Order = 7,
                Name = "Reporting & Compliance",
                Goal = "Periodic reports, statutory filings, audit-ready history.",
                NextStage = CaseStage.Closure,
                RequiredTasks = new[]
          {
      "Generate periodic reports",
        "Internal review and sign-off",
     "Compliance checks",
     },
                RequiredDocTypes = new[] { "periodic_report" },
                ValidationRules = new[]
            {
   new ValidationRule("Reports delivered on schedule", ctx => true), // stub
       new ValidationRule("No critical overdue compliance tasks", ctx => ctx.NoCriticalOverdueTasks()),
    },
                AutoTaskCategories = new[] { "Report", "Compliance", "Review" },
                TemplateTypes = Array.Empty<DocumentTemplateType>(),
            },

            [CaseStage.Closure] = new()
            {
                Stage = CaseStage.Closure,
                Order = 8,
                Name = "Closure",
                Goal = "Close case, archive, final notices, retention.",
                NextStage = null,
                RequiredTasks = new[]
          {
     "Final account statement",
  "Final report and distribution summary",
               "Final notices",
 "Archive all documents",
          "Post-case QA checklist",
           },
                RequiredDocTypes = new[] { "final_report", "closure_notice" },
                ValidationRules = new[]
      {
        new ValidationRule("All closure documents generated and sent", ctx => ctx.HasDocType("final_report")),
         new ValidationRule("No open critical tasks", ctx => ctx.NoCriticalOverdueTasks()),
     new ValidationRule("Archive completed", ctx => true), // stub
                },
                AutoTaskCategories = new[] { "Document", "Email", "Compliance" },
                TemplateTypes = new[] { DocumentTemplateType.FinalReportArt167 },
            },
        };

    /// <summary>Get the stage definition or null.</summary>
    public static StageDefinition? GetStage(CaseStage stage) =>
          Stages.TryGetValue(stage, out var def) ? def : null;

    /// <summary>Get the ordered list of stages.</summary>
    public static IReadOnlyList<StageDefinition> GetOrderedStages() =>
        Stages.Values.OrderBy(s => s.Order).ToList();
}

/// <summary>Definition of a single workflow stage.</summary>
public class StageDefinition
{
    public CaseStage Stage { get; init; }
    public int Order { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Goal { get; init; } = string.Empty;
    public CaseStage? NextStage { get; init; }
    public string[] RequiredTasks { get; init; } = Array.Empty<string>();
    public string[] RequiredDocTypes { get; init; } = Array.Empty<string>();
    public ValidationRule[] ValidationRules { get; init; } = Array.Empty<ValidationRule>();
    public string[] AutoTaskCategories { get; init; } = Array.Empty<string>();
    public DocumentTemplateType[] TemplateTypes { get; init; } = Array.Empty<DocumentTemplateType>();
}

/// <summary>A single validation rule that must pass before advancing to the next stage.</summary>
public class ValidationRule
{
    public string Description { get; }
    public Func<ValidationContext, bool> Predicate { get; }

    public ValidationRule(string description, Func<ValidationContext, bool> predicate)
    {
        Description = description;
        Predicate = predicate;
    }
}

/// <summary>Context object passed to validation rules for stage gate checks.</summary>
public class ValidationContext
{
    public required Domain.Entities.InsolvencyCase Case { get; init; }
    public IReadOnlyList<Domain.Entities.CaseParty> Parties { get; init; } = Array.Empty<Domain.Entities.CaseParty>();
    public IReadOnlyList<Domain.Entities.InsolvencyDocument> Documents { get; init; } = Array.Empty<Domain.Entities.InsolvencyDocument>();
    public IReadOnlyList<Domain.Entities.CompanyTask> Tasks { get; init; } = Array.Empty<Domain.Entities.CompanyTask>();
    public IReadOnlyList<Domain.Entities.ScheduledEmail> Emails { get; init; } = Array.Empty<Domain.Entities.ScheduledEmail>();

    // ── Helper methods used by validation predicates ──

    public bool PartyRolesPresent(params string[] roles) =>
        roles.All(r => Parties.Any(p => p.Role.ToString().Equals(r, StringComparison.OrdinalIgnoreCase)));

    public bool HasDocType(string docType) =>
        Documents.Any(d => d.DocType.Equals(docType, StringComparison.OrdinalIgnoreCase));

    public bool HasTemplateGenerated(DocumentTemplateType tt) =>
        Documents.Any(d => d.DocType.Equals(tt.ToString(), StringComparison.OrdinalIgnoreCase));

    public bool KeyPartiesHaveDeliveryMethod() =>
        Parties.Where(p => p.Role is CasePartyRole.Debtor or CasePartyRole.JudgeSyndic or CasePartyRole.InsolvencyPractitioner)
            .All(p => !string.IsNullOrWhiteSpace(p.Email) || !string.IsNullOrWhiteSpace(p.Address));

    public bool AllRequiredNoticesSent() =>
 Emails.Any(e => e.IsSent);

    public bool NoUnresolvedDeliveryFailures() =>
      !Emails.Any(e => !e.IsSent && e.RetryCount > 2);

    public bool AllClaimsReviewed() =>
    !Tasks.Any(t => t.Title.Contains("Review claim", StringComparison.OrdinalIgnoreCase)
        && t.Status != Domain.Enums.TaskStatus.Done);

    public bool MeetingNoticesSentOnTime() =>
     Emails.Any(e => e.IsSent && e.Subject != null && e.Subject.Contains("meeting", StringComparison.OrdinalIgnoreCase));

    public bool NoCriticalOverdueTasks() =>
     !Tasks.Any(t => t.IsCriticalDeadline && t.Deadline < DateTime.UtcNow && t.Status != Domain.Enums.TaskStatus.Done);
}
