using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

public sealed class CaseService : ICaseService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CaseService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
      _audit = audit;
    }

    public async Task<List<CaseDto>> GetAllAsync(Guid? companyId, CancellationToken ct)
 {
        var tenantId = _currentUser.TenantId;
        var query = _db.InsolvencyCases
        .Include(c => c.Company)
   .Include(c => c.AssignedTo)
     .Where(c => tenantId == null || c.TenantId == tenantId);

        if (companyId.HasValue)
  query = query.Where(c => c.CompanyId == companyId);

        var cases = await query.OrderByDescending(c => c.CreatedOn).ToListAsync(ct);

        var caseIds = cases.Select(c => c.Id).ToList();
        var docCounts = await _db.InsolvencyDocuments
     .Where(d => caseIds.Contains(d.CaseId))
  .GroupBy(d => d.CaseId)
            .Select(g => new { g.Key, Count = g.Count() })
   .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

      var partyCounts = await _db.CaseParties
       .Where(p => caseIds.Contains(p.CaseId))
            .GroupBy(p => p.CaseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return cases.Select(c => c.ToDto(
        docCounts.GetValueOrDefault(c.Id, 0),
       partyCounts.GetValueOrDefault(c.Id, 0)
        )).ToList();
    }

    public async Task<CaseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
   var c = await _db.InsolvencyCases
            .Include(x => x.Company)
      .Include(x => x.AssignedTo)
   .Include(x => x.Documents)
            .Include(x => x.Phases)
         .Include(x => x.Parties)
.FirstOrDefaultAsync(x => x.Id == id && (tenantId == null || x.TenantId == tenantId), ct);

        if (c is null) return null;

   var phases = c.Phases.OrderBy(p => p.SortOrder).Select(p => p.ToDto()).ToList();
        return c.ToDto(c.Documents.Count, c.Parties.Count, phases);
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(Guid caseId, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        return await _db.InsolvencyDocuments
         .Where(d => d.CaseId == caseId && (tenantId == null || d.TenantId == tenantId))
            .OrderByDescending(d => d.UploadedAt)
.Select(d => d.ToDto())
            .ToListAsync(ct);
  }

    public async Task<CaseDto> CreateAsync(CreateCaseCommand command, CancellationToken ct)
    {
      var tenantId = _currentUser.TenantId
      ?? throw new BusinessException("Tenant context is required to create a case.");

     var insolvencyCase = new InsolvencyCase
        {
Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseNumber = command.CaseNumber,
            CourtName = command.CourtName,
            CourtSection = command.CourtSection,
       DebtorName = command.DebtorName,
    DebtorCui = command.DebtorCui,
            ProcedureType = command.ProcedureType ?? ProcedureType.Other,
 Stage = CaseStage.Intake,
            StageEnteredAt = DateTime.UtcNow,
       LawReference = command.LawReference,
     CompanyId = command.CompanyId,
            CreatedOn = DateTime.UtcNow,
  CreatedBy = _currentUser.Email ?? "System",
  };

        _db.InsolvencyCases.Add(insolvencyCase);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
            Action = "Case.Created",
   Description = $"A new insolvency case '{insolvencyCase.CaseNumber}' was created for debtor '{insolvencyCase.DebtorName}'.",
            EntityType = "InsolvencyCase",
       EntityId = insolvencyCase.Id,
            EntityName = insolvencyCase.CaseNumber,
         CaseNumber = insolvencyCase.CaseNumber,
         NewValues = new { insolvencyCase.CaseNumber, insolvencyCase.DebtorName, insolvencyCase.ProcedureType },
            Severity = "Info",
       Category = "CaseManagement",
});

        return insolvencyCase.ToDto();
    }

    public async Task<CaseDto> UpdateAsync(Guid id, UpdateCaseCommand cmd, CancellationToken ct)
  {
    var tenantId = _currentUser.TenantId;
        var c = await _db.InsolvencyCases
.FirstOrDefaultAsync(x => x.Id == id && (tenantId == null || x.TenantId == tenantId), ct)
  ?? throw new BusinessException($"Case {id} not found.");

        var oldStage = c.Stage;

        if (cmd.CaseNumber != null) c.CaseNumber = cmd.CaseNumber;
        if (cmd.CourtName != null) c.CourtName = cmd.CourtName;
        if (cmd.CourtSection != null) c.CourtSection = cmd.CourtSection;
        if (cmd.JudgeSyndic != null) c.JudgeSyndic = cmd.JudgeSyndic;
   if (cmd.ProcedureType.HasValue) c.ProcedureType = cmd.ProcedureType.Value;
        if (cmd.Stage.HasValue)
   {
            c.Stage = cmd.Stage.Value;
            if (oldStage != cmd.Stage.Value)
            {
                c.StageCompletedAt = DateTime.UtcNow;
      c.StageEnteredAt = DateTime.UtcNow;
   }
        }
        if (cmd.LawReference != null) c.LawReference = cmd.LawReference;
      if (cmd.PractitionerName != null) c.PractitionerName = cmd.PractitionerName;
      if (cmd.PractitionerRole != null) c.PractitionerRole = cmd.PractitionerRole;
        if (cmd.PractitionerFiscalId != null) c.PractitionerFiscalId = cmd.PractitionerFiscalId;
      if (cmd.PractitionerDecisionNo != null) c.PractitionerDecisionNo = cmd.PractitionerDecisionNo;
        if (cmd.NoticeDate.HasValue) c.NoticeDate = cmd.NoticeDate;
        if (cmd.OpeningDate.HasValue) c.OpeningDate = cmd.OpeningDate;
      if (cmd.NextHearingDate.HasValue) c.NextHearingDate = cmd.NextHearingDate;
  if (cmd.ClaimsDeadline.HasValue) c.ClaimsDeadline = cmd.ClaimsDeadline;
        if (cmd.ContestationsDeadline.HasValue) c.ContestationsDeadline = cmd.ContestationsDeadline;
        if (cmd.DefinitiveTableDate.HasValue) c.DefinitiveTableDate = cmd.DefinitiveTableDate;
        if (cmd.ReorganizationPlanDeadline.HasValue) c.ReorganizationPlanDeadline = cmd.ReorganizationPlanDeadline;
        if (cmd.ClosureDate.HasValue) c.ClosureDate = cmd.ClosureDate;
 if (cmd.TotalClaimsRon.HasValue) c.TotalClaimsRon = cmd.TotalClaimsRon;
        if (cmd.SecuredClaimsRon.HasValue) c.SecuredClaimsRon = cmd.SecuredClaimsRon;
        if (cmd.UnsecuredClaimsRon.HasValue) c.UnsecuredClaimsRon = cmd.UnsecuredClaimsRon;
      if (cmd.BudgetaryClaimsRon.HasValue) c.BudgetaryClaimsRon = cmd.BudgetaryClaimsRon;
    if (cmd.EmployeeClaimsRon.HasValue) c.EmployeeClaimsRon = cmd.EmployeeClaimsRon;
      if (cmd.EstimatedAssetValueRon.HasValue) c.EstimatedAssetValueRon = cmd.EstimatedAssetValueRon;
        if (cmd.BpiPublicationNo != null) c.BpiPublicationNo = cmd.BpiPublicationNo;
        if (cmd.BpiPublicationDate.HasValue) c.BpiPublicationDate = cmd.BpiPublicationDate;
        if (cmd.OpeningDecisionNo != null) c.OpeningDecisionNo = cmd.OpeningDecisionNo;
   if (cmd.Notes != null) c.Notes = cmd.Notes;
   if (cmd.CompanyId.HasValue) c.CompanyId = cmd.CompanyId;
        if (cmd.AssignedToUserId.HasValue) c.AssignedToUserId = cmd.AssignedToUserId;

        c.LastModifiedOn = DateTime.UtcNow;
        c.LastModifiedBy = _currentUser.Email;

     await _db.SaveChangesAsync(ct);

     var description = oldStage != c.Stage
            ? $"Case '{c.CaseNumber}' was updated and transitioned from stage '{oldStage}' to '{c.Stage}'."
  : $"Case '{c.CaseNumber}' details were updated.";

  await _audit.LogAsync(new AuditEntry
   {
            Action = oldStage != c.Stage ? "Case.StageTransitioned" : "Case.Updated",
            Description = description,
       EntityType = "InsolvencyCase",
        EntityId = c.Id,
   EntityName = c.CaseNumber,
CaseNumber = c.CaseNumber,
         Severity = oldStage != c.Stage ? "Warning" : "Info",
Category = "CaseManagement",
        });

        return c.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var c = await _db.InsolvencyCases
            .FirstOrDefaultAsync(x => x.Id == id && (tenantId == null || x.TenantId == tenantId), ct)
            ?? throw new BusinessException($"Case {id} not found.");

        _db.InsolvencyCases.Remove(c);
   await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
    {
Action = "Case.Deleted",
            Description = $"Insolvency case '{c.CaseNumber}' for debtor '{c.DebtorName}' was permanently deleted.",
   EntityType = "InsolvencyCase",
            EntityId = id,
   EntityName = c.CaseNumber,
        CaseNumber = c.CaseNumber,
   Severity = "Critical",
            Category = "CaseManagement",
        });
  }
}
