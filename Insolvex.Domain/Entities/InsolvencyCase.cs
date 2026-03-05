using Insolvex.Domain.Enums;

namespace Insolvex.Domain.Entities;

public class InsolvencyCase : TenantScopedEntity
{
    // ?? Case identification ??
    public string CaseNumber { get; set; } = string.Empty;
    public string? CourtName { get; set; }
    public string? CourtSection { get; set; }
    public string? JudgeSyndic { get; set; }
    /// <summary>Court registrar (Grefier) assigned to this case</summary>
    public string? Registrar { get; set; }

    // ?? Debtor info (denormalized for quick display) ??
    public string DebtorName { get; set; } = string.Empty;
    public string? DebtorCui { get; set; }

    // ── Procedure ──
    public ProcedureType ProcedureType { get; set; } = ProcedureType.Other;
    /// <summary>Active, Suspended, Closed, Cancelled</summary>
    public string Status { get; set; } = "Active";
    public string? LawReference { get; set; }

    // ?? Practitioner info (denormalized) ??
    public string? PractitionerName { get; set; }
    public string? PractitionerRole { get; set; }
    public string? PractitionerFiscalId { get; set; }
    public string? PractitionerDecisionNo { get; set; }

    // ?? Key dates ??
    /// <summary>Source of truth: extracted from the original notice document. Equals CaseCreationDate.</summary>
    public DateTime? NoticeDate { get; set; }
    public DateTime? OpeningDate { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public DateTime? ClaimsDeadline { get; set; }
    public DateTime? ContestationsDeadline { get; set; }
    public DateTime? DefinitiveTableDate { get; set; }
    public DateTime? ReorganizationPlanDeadline { get; set; }
    public DateTime? ClosureDate { get; set; }

    // ── Stage tracking ──
    /// <summary>When the status was last changed.</summary>
    public DateTime? StatusChangedAt { get; set; }

    /// <summary>Mandatory explanation when closing a case (especially when stages are force-overridden).</summary>
    public string? ClosureNotes { get; set; }

    // ?? Structured key deadlines (JSON) ??
    /// <summary>JSON object with typed deadline keys: claimDeadline, objectionDeadline, meetingDate, reportDates, etc.</summary>
    public string? KeyDeadlinesJson { get; set; }

    // ?? Financial summary ??
    public decimal? TotalClaimsRon { get; set; }
    public decimal? SecuredClaimsRon { get; set; }
    public decimal? UnsecuredClaimsRon { get; set; }
    public decimal? BudgetaryClaimsRon { get; set; }
    public decimal? EmployeeClaimsRon { get; set; }
    public decimal? EstimatedAssetValueRon { get; set; }

    // ?? BPI (Buletinul Procedurilor de Insolventa) ??
    /// <summary>BPI publication number for opening</summary>
    public string? BpiPublicationNo { get; set; }
    /// <summary>Date of BPI publication</summary>
    public DateTime? BpiPublicationDate { get; set; }

    // ?? Additional references ??
    /// <summary>Court decision number that opened the procedure</summary>
    public string? OpeningDecisionNo { get; set; }
    /// <summary>Notification document sequential number (separate from OpeningDecisionNo)</summary>
    public string? NotificationNumber { get; set; }
    /// <summary>Notes / internal observations</summary>
    public string? Notes { get; set; }

    // ?? Court registry info (used in Notificare template) ??
    /// <summary>Physical address of the court registry office</summary>
    public string? CourtRegistryAddress { get; set; }
    /// <summary>Phone number of the court registry office</summary>
    public string? CourtRegistryPhone { get; set; }
    /// <summary>Opening hours of the court registry, e.g. "Luni–Vineri, 08:00–12:00"</summary>
    public string? CourtRegistryHours { get; set; }

    // ?? Debtor details ??
    /// <summary>Name of the debtor's legal administrator / representative at the time of opening</summary>
    public string? DebtorAdministratorName { get; set; }

    // ?? Creditors meeting ??
    /// <summary>Physical address where the first creditors meeting will take place</summary>
    public string? CreditorsMeetingAddress { get; set; }
    /// <summary>Time of the first creditors meeting, e.g. "12:00"</summary>
    public string? CreditorsMeetingTime { get; set; }

    // ?? Court stamp tax ??
    /// <summary>Judicial stamp tax amount for this notification, e.g. "200,00 lei"</summary>
    public string? CourtTaxStampAmount { get; set; }

    // ?? Relationships ??
    public Guid? CompanyId { get; set; }
    public virtual Company? Company { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public virtual User? AssignedTo { get; set; }

    // Navigation
    public ICollection<InsolvencyDocument> Documents { get; set; } = new List<InsolvencyDocument>();
    public ICollection<CaseParty> Parties { get; set; } = new List<CaseParty>();
    public ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
    public ICollection<CaseSummary> Summaries { get; set; } = new List<CaseSummary>();
    public ICollection<CompanyTask> Tasks { get; set; } = new List<CompanyTask>();
    public ICollection<ScheduledEmail> Emails { get; set; } = new List<ScheduledEmail>();
    public ICollection<GeneratedLetter> GeneratedLetters { get; set; } = new List<GeneratedLetter>();
    public ICollection<CaseDeadlineOverride> DeadlineOverrides { get; set; } = new List<CaseDeadlineOverride>();
    public ICollection<CaseEvent> Events { get; set; } = new List<CaseEvent>();
    public ICollection<CreditorClaim> Claims { get; set; } = new List<CreditorClaim>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<CaseWorkflowStage> WorkflowStages { get; set; } = new List<CaseWorkflowStage>();
}
