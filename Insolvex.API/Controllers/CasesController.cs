using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CaseView)]
public class CasesController : ControllerBase
{
 private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public CasesController(ApplicationDbContext db, IAuditService audit)
    {
     _db = db;
        _audit = audit;
    }

 [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId = null)
    {
        var query = _db.InsolvencyCases
      .Include(c => c.Company)
 .Include(c => c.AssignedTo)
            .AsQueryable();

        if (companyId.HasValue)
     query = query.Where(c => c.CompanyId == companyId);

        var cases = await query.OrderByDescending(c => c.CreatedOn).ToListAsync();

        var caseIds = cases.Select(c => c.Id).ToList();
        var docCounts = await _db.InsolvencyDocuments
     .Where(d => caseIds.Contains(d.CaseId))
            .GroupBy(d => d.CaseId)
 .Select(g => new { CaseId = g.Key, Count = g.Count() })
   .ToDictionaryAsync(x => x.CaseId, x => x.Count);

var partyCounts = await _db.CaseParties
            .Where(p => caseIds.Contains(p.CaseId))
            .GroupBy(p => p.CaseId)
            .Select(g => new { CaseId = g.Key, Count = g.Count() })
          .ToDictionaryAsync(x => x.CaseId, x => x.Count);

        var dtos = cases.Select(c => c.ToDto(
     docCounts.GetValueOrDefault(c.Id, 0),
       partyCounts.GetValueOrDefault(c.Id, 0)
   )).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.InsolvencyCases
      .Include(x => x.Company)
 .Include(x => x.AssignedTo)
   .Include(x => x.Documents)
   .Include(x => x.Phases)
          .Include(x => x.Parties)
   .FirstOrDefaultAsync(x => x.Id == id);
      if (c == null) return NotFound();

        var phases = c.Phases.OrderBy(p => p.SortOrder).Select(p => p.ToDto()).ToList();
        return Ok(c.ToDto(c.Documents.Count, c.Parties.Count, phases));
    }

    [HttpGet("{id:guid}/documents")]
  public async Task<IActionResult> GetDocuments(Guid id)
    {
  var docs = await _db.InsolvencyDocuments
   .Where(d => d.CaseId == id)
    .OrderByDescending(d => d.UploadedAt)
       .Select(d => d.ToDto())
         .ToListAsync();
      return Ok(docs);
    }

  [HttpPost]
    [RequirePermission(Permission.CaseCreate)]
    public async Task<IActionResult> Create([FromBody] CreateCaseRequest request)
    {
  var procedureType = request.ProcedureType ?? Domain.Enums.ProcedureType.Other;

        var insolvencyCase = new InsolvencyCase
  {
        Id = Guid.NewGuid(),
    CaseNumber = request.CaseNumber,
            CourtName = request.CourtName,
       CourtSection = request.CourtSection,
  DebtorName = request.DebtorName,
      DebtorCui = request.DebtorCui,
     ProcedureType = procedureType,
    LawReference = request.LawReference,
            CompanyId = request.CompanyId
        };

        _db.InsolvencyCases.Add(insolvencyCase);
      await _db.SaveChangesAsync();
      await _audit.LogAsync("Case.Created", insolvencyCase.Id);
  return CreatedAtAction(nameof(GetById), new { id = insolvencyCase.Id }, insolvencyCase.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCaseRequest request)
    {
     var c = await _db.InsolvencyCases.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        if (request.CaseNumber != null) c.CaseNumber = request.CaseNumber;
    if (request.CourtName != null) c.CourtName = request.CourtName;
     if (request.CourtSection != null) c.CourtSection = request.CourtSection;
        if (request.JudgeSyndic != null) c.JudgeSyndic = request.JudgeSyndic;
        if (request.ProcedureType.HasValue) c.ProcedureType = request.ProcedureType.Value;
        if (request.Stage.HasValue) c.Stage = request.Stage.Value;
        if (request.LawReference != null) c.LawReference = request.LawReference;
  if (request.PractitionerName != null) c.PractitionerName = request.PractitionerName;
        if (request.PractitionerRole != null) c.PractitionerRole = request.PractitionerRole;
        if (request.PractitionerFiscalId != null) c.PractitionerFiscalId = request.PractitionerFiscalId;
        if (request.PractitionerDecisionNo != null) c.PractitionerDecisionNo = request.PractitionerDecisionNo;
        if (request.OpeningDate.HasValue) c.OpeningDate = request.OpeningDate;
        if (request.NextHearingDate.HasValue) c.NextHearingDate = request.NextHearingDate;
        if (request.ClaimsDeadline.HasValue) c.ClaimsDeadline = request.ClaimsDeadline;
        if (request.ContestationsDeadline.HasValue) c.ContestationsDeadline = request.ContestationsDeadline;
        if (request.DefinitiveTableDate.HasValue) c.DefinitiveTableDate = request.DefinitiveTableDate;
    if (request.ReorganizationPlanDeadline.HasValue) c.ReorganizationPlanDeadline = request.ReorganizationPlanDeadline;
        if (request.ClosureDate.HasValue) c.ClosureDate = request.ClosureDate;
        if (request.TotalClaimsRon.HasValue) c.TotalClaimsRon = request.TotalClaimsRon;
        if (request.SecuredClaimsRon.HasValue) c.SecuredClaimsRon = request.SecuredClaimsRon;
        if (request.UnsecuredClaimsRon.HasValue) c.UnsecuredClaimsRon = request.UnsecuredClaimsRon;
        if (request.BudgetaryClaimsRon.HasValue) c.BudgetaryClaimsRon = request.BudgetaryClaimsRon;
        if (request.EmployeeClaimsRon.HasValue) c.EmployeeClaimsRon = request.EmployeeClaimsRon;
     if (request.EstimatedAssetValueRon.HasValue) c.EstimatedAssetValueRon = request.EstimatedAssetValueRon;
        if (request.BpiPublicationNo != null) c.BpiPublicationNo = request.BpiPublicationNo;
        if (request.BpiPublicationDate.HasValue) c.BpiPublicationDate = request.BpiPublicationDate;
        if (request.OpeningDecisionNo != null) c.OpeningDecisionNo = request.OpeningDecisionNo;
     if (request.Notes != null) c.Notes = request.Notes;
        if (request.CompanyId.HasValue) c.CompanyId = request.CompanyId;
     if (request.AssignedToUserId.HasValue) c.AssignedToUserId = request.AssignedToUserId;

  await _db.SaveChangesAsync();
        await _audit.LogAsync("Case.Updated", c.Id);
      return Ok(c.ToDto());
    }

    [HttpDelete("{id:guid}")]
 [RequirePermission(Permission.CaseDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
   var c = await _db.InsolvencyCases.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

    _db.InsolvencyCases.Remove(c);
        await _db.SaveChangesAsync();
     await _audit.LogAsync("Case.Deleted", id);
 return NoContent();
    }
}
