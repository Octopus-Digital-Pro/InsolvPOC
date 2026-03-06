using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for insolvency case lifecycle management.
/// All operations are tenant-scoped and audited.
/// </summary>
public interface ICaseService
{
  /// <summary>List all cases, optionally filtered by linked company.</summary>
  Task<List<CaseDto>> GetAllAsync(Guid? companyId = null, CancellationToken ct = default);

  /// <summary>Retrieve a single case with phases, documents, and parties.</summary>
  Task<CaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>Retrieve all documents linked to a case.</summary>
  Task<List<DocumentDto>> GetDocumentsAsync(Guid caseId, CancellationToken ct = default);

  /// <summary>Create a new insolvency case at the Intake stage.</summary>
  Task<CaseDto> CreateAsync(CreateCaseCommand command, CancellationToken ct = default);

  /// <summary>Update case fields (partial patch).</summary>
  Task<CaseDto> UpdateAsync(Guid id, UpdateCaseCommand command, CancellationToken ct = default);

  /// <summary>Permanently delete a case.</summary>
  Task DeleteAsync(Guid id, CancellationToken ct = default);

  /// <summary>Export all cases as CSV bytes.</summary>
  Task<byte[]> ExportCsvAsync(CancellationToken ct = default);

  /// <summary>Download all documents for a case as a ZIP stream.</summary>
  Task<(Stream Stream, string FileName)> DownloadDocumentsZipAsync(Guid caseId, CancellationToken ct = default);
}

public class CreateCaseCommand
{
  public string CaseNumber { get; init; } = string.Empty;
  public string? CourtName { get; init; }
  public string? CourtSection { get; init; }
  public string DebtorName { get; init; } = string.Empty;
  public string? DebtorCui { get; init; }
  public ProcedureType? ProcedureType { get; init; }
  public string? LawReference { get; init; }
  public Guid? CompanyId { get; init; }
  // Key dates (optional on creation)
  public DateTime? NoticeDate { get; init; }
  public DateTime? OpeningDate { get; init; }
  public DateTime? NextHearingDate { get; init; }
  public DateTime? ClaimsDeadline { get; init; }
  public DateTime? ContestationsDeadline { get; init; }
  public DateTime? DefinitiveTableDate { get; init; }
  public DateTime? ReorganizationPlanDeadline { get; init; }
}

public class UpdateCaseCommand
{
  public string? CaseNumber { get; init; }
  public string? CourtName { get; init; }
  public string? CourtSection { get; init; }
  public string? JudgeSyndic { get; init; }
  public string? Registrar { get; init; }
  public ProcedureType? ProcedureType { get; init; }
  public string? Status { get; init; }
  public string? LawReference { get; init; }
  public string? PractitionerName { get; init; }
  public string? PractitionerRole { get; init; }
  public string? PractitionerFiscalId { get; init; }
  public string? PractitionerDecisionNo { get; init; }
  public DateTime? NoticeDate { get; init; }
  public DateTime? OpeningDate { get; init; }
  public DateTime? NextHearingDate { get; init; }
  public DateTime? ClaimsDeadline { get; init; }
  public DateTime? ContestationsDeadline { get; init; }
  public DateTime? DefinitiveTableDate { get; init; }
  public DateTime? ReorganizationPlanDeadline { get; init; }
  public DateTime? ClosureDate { get; init; }
  public decimal? TotalClaimsRon { get; init; }
  public decimal? SecuredClaimsRon { get; init; }
  public decimal? UnsecuredClaimsRon { get; init; }
  public decimal? BudgetaryClaimsRon { get; init; }
  public decimal? EmployeeClaimsRon { get; init; }
  public decimal? EstimatedAssetValueRon { get; init; }
  public string? BpiPublicationNo { get; init; }
  public DateTime? BpiPublicationDate { get; init; }
  public string? OpeningDecisionNo { get; init; }
  public string? Notes { get; init; }
  public Guid? CompanyId { get; init; }
  public Guid? AssignedToUserId { get; init; }
}
