using Insolvex.Domain.Enums;

namespace Insolvex.Domain.Entities;

public class InsolvencyCase : TenantScopedEntity
{
    // ?? Case identification ??
    public string CaseNumber { get; set; } = string.Empty;
    public string? CourtName { get; set; }
    public string? CourtSection { get; set; }
    public string? JudgeSyndic { get; set; }

    // ?? Debtor info (denormalized for quick display) ??
    public string DebtorName { get; set; } = string.Empty;
    public string? DebtorCui { get; set; }

    // ?? Procedure ??
    public ProcedureType ProcedureType { get; set; } = ProcedureType.Other;
    public CaseStage Stage { get; set; } = CaseStage.Intake;
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

    // ?? Stage tracking ??
    /// <summary>When the current stage was entered.</summary>
    public DateTime? StageEnteredAt { get; set; }
    /// <summary>When the current stage was completed (before advancing).</summary>
    public DateTime? StageCompletedAt { get; set; }

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
    /// <summary>Notes / internal observations</summary>
    public string? Notes { get; set; }

    // ?? Relationships ??
    public Guid? CompanyId { get; set; }
    public virtual Company? Company { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public virtual User? AssignedTo { get; set; }

    // Navigation
    public ICollection<InsolvencyDocument> Documents { get; set; } = new List<InsolvencyDocument>();
    public ICollection<CaseParty> Parties { get; set; } = new List<CaseParty>();
    public ICollection<CasePhase> Phases { get; set; } = new List<CasePhase>();
    public ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
    public ICollection<CaseSummary> Summaries { get; set; } = new List<CaseSummary>();
}
