using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

public record CaseDto(
    Guid Id,
    string CaseNumber,
    string? CourtName,
    string? CourtSection,
    string? JudgeSyndic,
    string? Registrar,
    string DebtorName,
    string? DebtorCui,
    ProcedureType ProcedureType,
    string Status,
    string? LawReference,
    string? PractitionerName,
    string? PractitionerRole,
    string? PractitionerFiscalId,
    string? PractitionerDecisionNo,
    DateTime? NoticeDate,
    DateTime? OpeningDate,
    DateTime? NextHearingDate,
    DateTime? ClaimsDeadline,
    DateTime? ContestationsDeadline,
    DateTime? DefinitiveTableDate,
    DateTime? ReorganizationPlanDeadline,
    DateTime? ClosureDate,
    DateTime? StatusChangedAt,
    decimal? TotalClaimsRon,
    decimal? SecuredClaimsRon,
    decimal? UnsecuredClaimsRon,
    decimal? BudgetaryClaimsRon,
    decimal? EmployeeClaimsRon,
    decimal? EstimatedAssetValueRon,
    string? BpiPublicationNo,
    DateTime? BpiPublicationDate,
    string? OpeningDecisionNo,
    string? Notes,
    Guid? CompanyId,
    string? CompanyName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedOn,
    int DocumentCount,
    int PartyCount
);

public record CreateCaseRequest(
    string CaseNumber,
    string? CourtName,
    string? CourtSection,
    string DebtorName,
    string? DebtorCui,
    ProcedureType? ProcedureType,
    string? LawReference,
    Guid? CompanyId
);

public record UpdateCaseRequest(
    string? CaseNumber,
    string? CourtName,
    string? CourtSection,
    string? JudgeSyndic,
    string? Registrar,
    ProcedureType? ProcedureType,
    string? Status,
    string? LawReference,
    string? PractitionerName,
    string? PractitionerRole,
    string? PractitionerFiscalId,
    string? PractitionerDecisionNo,
    DateTime? NoticeDate,
    DateTime? OpeningDate,
    DateTime? NextHearingDate,
    DateTime? ClaimsDeadline,
    DateTime? ContestationsDeadline,
    DateTime? DefinitiveTableDate,
    DateTime? ReorganizationPlanDeadline,
    DateTime? ClosureDate,
    decimal? TotalClaimsRon,
    decimal? SecuredClaimsRon,
    decimal? UnsecuredClaimsRon,
    decimal? BudgetaryClaimsRon,
    decimal? EmployeeClaimsRon,
    decimal? EstimatedAssetValueRon,
    string? BpiPublicationNo,
    DateTime? BpiPublicationDate,
    string? OpeningDecisionNo,
    string? Notes,
    Guid? CompanyId,
    Guid? AssignedToUserId
);

// ── Feature 4: Procedure Type Change ─────────────────────────────────────────

public class ChangeProcedureTypeCommand
{
    public ProcedureType NewProcedureType { get; set; }
    /// <summary>Mandatory reason documented in history and audit trail.</summary>
    public string Reason { get; set; } = string.Empty;
}

public record ChangeProcedureTypeResult(
    List<string> RemovedStages,
    List<string> AddedStages,
    int PreservedTasks);

public record ProcedureHistoryDto(
    Guid Id,
    ProcedureType OldProcedureType,
    ProcedureType NewProcedureType,
    DateTime ChangedAt,
    string? ChangedByName,
    string? Reason,
    string? WorkflowStagesRemovedJson);

