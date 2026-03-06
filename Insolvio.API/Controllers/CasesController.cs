using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CaseView)]
public class CasesController : ControllerBase
{
  private readonly ICaseService _cases;

  public CasesController(ICaseService cases) => _cases = cases;

  [HttpGet]
  public async Task<IActionResult> GetAll([FromQuery] Guid? companyId, CancellationToken ct)
      => Ok(await _cases.GetAllAsync(companyId, ct));

  [HttpGet("{id:guid}")]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var dto = await _cases.GetByIdAsync(id, ct);
    return dto is null ? NotFound() : Ok(dto);
  }

  [HttpGet("{id:guid}/documents")]
  public async Task<IActionResult> GetDocuments(Guid id, CancellationToken ct)
      => Ok(await _cases.GetDocumentsAsync(id, ct));

  [HttpGet("export-csv")]
  public async Task<IActionResult> ExportCsv(CancellationToken ct)
  {
    var bytes = await _cases.ExportCsvAsync(ct);
    return File(bytes, "text/csv", $"cases_{DateTime.UtcNow:yyyyMMdd}.csv");
  }

  [HttpGet("{id:guid}/documents/download-zip")]
  public async Task<IActionResult> DownloadDocumentsZip(Guid id, CancellationToken ct)
  {
    var (stream, fileName) = await _cases.DownloadDocumentsZipAsync(id, ct);
    return File(stream, "application/zip", fileName);
  }

  [HttpPost]
  [RequirePermission(Permission.CaseCreate)]
  public async Task<IActionResult> Create([FromBody] CreateCaseBody body, CancellationToken ct)
  {
    var dto = await _cases.CreateAsync(new CreateCaseCommand
    {
      CaseNumber = body.CaseNumber,
      CourtName = body.CourtName,
      CourtSection = body.CourtSection,
      DebtorName = body.DebtorName,
      DebtorCui = body.DebtorCui,
      ProcedureType = body.ProcedureType,
      LawReference = body.LawReference,
      CompanyId = body.CompanyId,
      NoticeDate = body.NoticeDate,
      OpeningDate = body.OpeningDate,
      NextHearingDate = body.NextHearingDate,
      ClaimsDeadline = body.ClaimsDeadline,
      ContestationsDeadline = body.ContestationsDeadline,
      DefinitiveTableDate = body.DefinitiveTableDate,
      ReorganizationPlanDeadline = body.ReorganizationPlanDeadline,
    }, ct);
    return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCaseBody body, CancellationToken ct)
  {
    var dto = await _cases.UpdateAsync(id, new UpdateCaseCommand
    {
      CaseNumber = body.CaseNumber,
      CourtName = body.CourtName,
      CourtSection = body.CourtSection,
      JudgeSyndic = body.JudgeSyndic,
      Registrar = body.Registrar,
      ProcedureType = body.ProcedureType,
      Status = body.Status,
      LawReference = body.LawReference,
      PractitionerName = body.PractitionerName,
      PractitionerRole = body.PractitionerRole,
      PractitionerFiscalId = body.PractitionerFiscalId,
      PractitionerDecisionNo = body.PractitionerDecisionNo,
      NoticeDate = body.NoticeDate,
      OpeningDate = body.OpeningDate,
      NextHearingDate = body.NextHearingDate,
      ClaimsDeadline = body.ClaimsDeadline,
      ContestationsDeadline = body.ContestationsDeadline,
      DefinitiveTableDate = body.DefinitiveTableDate,
      ReorganizationPlanDeadline = body.ReorganizationPlanDeadline,
      ClosureDate = body.ClosureDate,
      TotalClaimsRon = body.TotalClaimsRon,
      SecuredClaimsRon = body.SecuredClaimsRon,
      UnsecuredClaimsRon = body.UnsecuredClaimsRon,
      BudgetaryClaimsRon = body.BudgetaryClaimsRon,
      EmployeeClaimsRon = body.EmployeeClaimsRon,
      EstimatedAssetValueRon = body.EstimatedAssetValueRon,
      BpiPublicationNo = body.BpiPublicationNo,
      BpiPublicationDate = body.BpiPublicationDate,
      OpeningDecisionNo = body.OpeningDecisionNo,
      Notes = body.Notes,
      CompanyId = body.CompanyId,
      AssignedToUserId = body.AssignedToUserId,
    }, ct);
    return Ok(dto);
  }

  [HttpDelete("{id:guid}")]
  [RequirePermission(Permission.CaseDelete)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    await _cases.DeleteAsync(id, ct);
    return NoContent();
  }
}

// ── API request bodies ────────────────────────────────────────────────────────

public record CreateCaseBody(
    string CaseNumber,
    string DebtorName,
    string? CourtName = null,
    string? CourtSection = null,
    string? DebtorCui = null,
    ProcedureType? ProcedureType = null,
    string? LawReference = null,
    Guid? CompanyId = null,
    // Key dates
    DateTime? NoticeDate = null,
    DateTime? OpeningDate = null,
    DateTime? NextHearingDate = null,
    DateTime? ClaimsDeadline = null,
    DateTime? ContestationsDeadline = null,
    DateTime? DefinitiveTableDate = null,
    DateTime? ReorganizationPlanDeadline = null);

public record UpdateCaseBody(
    string? CaseNumber = null, string? CourtName = null, string? CourtSection = null,
    string? JudgeSyndic = null, string? Registrar = null, ProcedureType? ProcedureType = null, string? Status = null,
    string? LawReference = null, string? PractitionerName = null, string? PractitionerRole = null,
    string? PractitionerFiscalId = null, string? PractitionerDecisionNo = null,
    DateTime? NoticeDate = null, DateTime? OpeningDate = null, DateTime? NextHearingDate = null,
    DateTime? ClaimsDeadline = null, DateTime? ContestationsDeadline = null,
    DateTime? DefinitiveTableDate = null, DateTime? ReorganizationPlanDeadline = null, DateTime? ClosureDate = null,
    decimal? TotalClaimsRon = null, decimal? SecuredClaimsRon = null, decimal? UnsecuredClaimsRon = null,
    decimal? BudgetaryClaimsRon = null, decimal? EmployeeClaimsRon = null, decimal? EstimatedAssetValueRon = null,
    string? BpiPublicationNo = null, DateTime? BpiPublicationDate = null, string? OpeningDecisionNo = null,
    string? Notes = null, Guid? CompanyId = null, Guid? AssignedToUserId = null);

