using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Data.Services;

public sealed class CaseService : ICaseService
{
     private readonly ApplicationDbContext _db;
     private readonly ICurrentUserService _currentUser;
     private readonly IAuditService _audit;
     private readonly IFileStorageService _storage;
     private readonly CaseCreationService _caseCreation;

     public CaseService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit, IFileStorageService storage, CaseCreationService caseCreation)
     {
          _db = db;
          _currentUser = currentUser;
          _audit = audit;
          _storage = storage;
          _caseCreation = caseCreation;
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
                .Include(x => x.Parties)
       .FirstOrDefaultAsync(x => x.Id == id && (tenantId == null || x.TenantId == tenantId), ct);

          if (c is null) return null;

          return c.ToDto(c.Documents.Count, c.Parties.Count);
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
               Status = "Active",
               StatusChangedAt = DateTime.UtcNow,
               LawReference = command.LawReference,
               CompanyId = command.CompanyId,
               NoticeDate = command.NoticeDate,
               OpeningDate = command.OpeningDate,
               NextHearingDate = command.NextHearingDate,
               ClaimsDeadline = command.ClaimsDeadline,
               ContestationsDeadline = command.ContestationsDeadline,
               DefinitiveTableDate = command.DefinitiveTableDate,
               ReorganizationPlanDeadline = command.ReorganizationPlanDeadline,
               CreatedOn = DateTime.UtcNow,
               CreatedBy = _currentUser.Email ?? "System",
          };

          _db.InsolvencyCases.Add(insolvencyCase);
          await _db.SaveChangesAsync(ct);

          await _audit.LogAsync(new AuditEntry
          {
               Action = "Insolvency Case Opened",
               Description = $"A new insolvency case '{insolvencyCase.CaseNumber}' was created for debtor '{insolvencyCase.DebtorName}'.",
               EntityType = "InsolvencyCase",
               EntityId = insolvencyCase.Id,
               EntityName = insolvencyCase.CaseNumber,
               CaseNumber = insolvencyCase.CaseNumber,
               NewValues = new { insolvencyCase.CaseNumber, insolvencyCase.DebtorName, insolvencyCase.ProcedureType },
               Severity = "Info",
               Category = "CaseManagement",
          });

          // Resolve or create linked company if not already supplied
          if (insolvencyCase.CompanyId is null && !string.IsNullOrWhiteSpace(insolvencyCase.DebtorName))
          {
               var companyId = await _caseCreation.ResolveOrCreateCompanyAsync(
                    insolvencyCase.DebtorName, insolvencyCase.DebtorCui, tenantId, _currentUser.Email, insolvencyCase.Id, ct);
               if (companyId.HasValue)
               {
                    insolvencyCase.CompanyId = companyId;
                    await _db.SaveChangesAsync(ct);
               }
          }

          // Generate intake tasks
          if (insolvencyCase.CompanyId.HasValue)
          {
               var userId = _currentUser.UserId;
               var tasks = _caseCreation.GenerateIntakeTasks(
                    insolvencyCase, insolvencyCase.CompanyId.Value, tenantId,
                    DateTime.UtcNow, new Dictionary<string, DateTime>(),
                    null, userId, _currentUser.Email ?? "System");
               if (tasks.Count > 0)
               {
                    _db.CompanyTasks.AddRange(tasks);
                    await _db.SaveChangesAsync(ct);
               }

               // Create initial mandatory report task based on tenant deadline settings
               var deadlineSettings = await _db.Set<Insolvex.Domain.Entities.TenantDeadlineSettings>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
               var reportIntervalDays = deadlineSettings?.ReportEveryNDays ?? 30;
               var reportTask = new Insolvex.Domain.Entities.CompanyTask
               {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = insolvencyCase.CompanyId.Value,
                    CaseId = insolvencyCase.Id,
                    Title = $"Send Mandatory Report — {insolvencyCase.DebtorName}",
                    Description = $"Generate and send the periodic mandatory report (every {reportIntervalDays} days) for case {insolvencyCase.CaseNumber}. " +
                                  "Open the Mandatory Report template, review, save to documents, and email to relevant parties.",
                    Category = "Report",
                    Deadline = DateTime.UtcNow.AddDays(reportIntervalDays),
                    DeadlineSource = "CompanyDefault",
                    IsCriticalDeadline = true,
                    Status = Insolvex.Domain.Enums.TaskStatus.Open,
                    AssignedToUserId = _currentUser.UserId,
                    CreatedByUserId = _currentUser.UserId,
               };
               _db.CompanyTasks.Add(reportTask);
               await _db.SaveChangesAsync(ct);
          }

          return insolvencyCase.ToDto();
     }

     public async Task<CaseDto> UpdateAsync(Guid id, UpdateCaseCommand cmd, CancellationToken ct)
     {
          var tenantId = _currentUser.TenantId;
          var c = await _db.InsolvencyCases
  .FirstOrDefaultAsync(x => x.Id == id && (tenantId == null || x.TenantId == tenantId), ct)
    ?? throw new BusinessException($"Case {id} not found.");

          var oldStatus = c.Status;

          if (cmd.CaseNumber != null) c.CaseNumber = cmd.CaseNumber;
          if (cmd.CourtName != null) c.CourtName = cmd.CourtName;
          if (cmd.CourtSection != null) c.CourtSection = cmd.CourtSection;
          if (cmd.JudgeSyndic != null) c.JudgeSyndic = cmd.JudgeSyndic;
          if (cmd.ProcedureType.HasValue) c.ProcedureType = cmd.ProcedureType.Value;
          if (cmd.Status != null)
          {
               c.Status = cmd.Status;
               if (oldStatus != cmd.Status)
               {
                    c.StatusChangedAt = DateTime.UtcNow;
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

          var description = oldStatus != c.Status
                 ? $"Case '{c.CaseNumber}' was updated and status changed from '{oldStatus}' to '{c.Status}'."
       : $"Case '{c.CaseNumber}' details were updated.";

          await _audit.LogAsync(new AuditEntry
          {
               Action = oldStatus != c.Status ? "Case Status Changed" : "Case Details Updated",
               Description = description,
               EntityType = "InsolvencyCase",
               EntityId = c.Id,
               EntityName = c.CaseNumber,
               CaseNumber = c.CaseNumber,
               Severity = oldStatus != c.Status ? "Warning" : "Info",
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
               Action = "Insolvency Case Deleted",
               Description = $"Insolvency case '{c.CaseNumber}' for debtor '{c.DebtorName}' was permanently deleted.",
               EntityType = "InsolvencyCase",
               EntityId = id,
               EntityName = c.CaseNumber,
               CaseNumber = c.CaseNumber,
               Severity = "Critical",
               Category = "CaseManagement",
          });
     }

     public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
     {
          var tenantId = _currentUser.TenantId;
          var cases = await _db.InsolvencyCases
              .AsNoTracking()
              .Where(c => tenantId == null || c.TenantId == tenantId)
              .OrderByDescending(c => c.CreatedOn)
              .ToListAsync(ct);

          using var writer = new StringWriter();
          using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
          csv.WriteRecords(cases.Select(c => new
          {
               c.CaseNumber,
               c.DebtorName,
               c.DebtorCui,
               ProcedureType = c.ProcedureType.ToString(),
               Stage = c.Status,
               c.CourtName,
               c.CourtSection,
               c.JudgeSyndic,
               c.PractitionerName,
               c.PractitionerRole,
               c.LawReference,
               c.OpeningDecisionNo,
               c.BpiPublicationNo,
               NoticeDate = c.NoticeDate?.ToString("yyyy-MM-dd"),
               OpeningDate = c.OpeningDate?.ToString("yyyy-MM-dd"),
               NextHearingDate = c.NextHearingDate?.ToString("yyyy-MM-dd"),
               ClaimsDeadline = c.ClaimsDeadline?.ToString("yyyy-MM-dd"),
               ContestationsDeadline = c.ContestationsDeadline?.ToString("yyyy-MM-dd"),
               ClosureDate = c.ClosureDate?.ToString("yyyy-MM-dd"),
               c.TotalClaimsRon,
               c.SecuredClaimsRon,
               c.UnsecuredClaimsRon,
               c.BudgetaryClaimsRon,
               c.EmployeeClaimsRon,
               c.EstimatedAssetValueRon,
               CreatedOn = c.CreatedOn.ToString("yyyy-MM-dd"),
          }));
          return Encoding.UTF8.GetBytes(writer.ToString());
     }

     public async Task<(Stream Stream, string FileName)> DownloadDocumentsZipAsync(Guid caseId, CancellationToken ct = default)
     {
          var tenantId = _currentUser.TenantId;
          var caseEntity = await _db.InsolvencyCases
              .AsNoTracking()
              .FirstOrDefaultAsync(c => c.Id == caseId && (tenantId == null || c.TenantId == tenantId), ct)
              ?? throw new NotFoundException("InsolvencyCase", caseId);

          var docs = await _db.InsolvencyDocuments
              .AsNoTracking()
              .Where(d => d.CaseId == caseId && !string.IsNullOrEmpty(d.StorageKey))
              .ToListAsync(ct);

          if (docs.Count == 0)
               throw new BusinessException("No documents with stored files found for this case.");

          var zipMs = new MemoryStream();
          using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
          {
               foreach (var doc in docs)
               {
                    try
                    {
                         var fileStream = await _storage.DownloadAsync(doc.StorageKey!, ct);
                         var entryName = $"{doc.DocType}_{doc.SourceFileName}";
                         var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                         await using var entryStream = entry.Open();
                         await fileStream.CopyToAsync(entryStream, ct);
                    }
                    catch { /* skip documents whose storage files are missing */ }
               }
          }

          zipMs.Seek(0, SeekOrigin.Begin);
          var safeName = caseEntity.CaseNumber.Replace("/", "-").Replace("\\", "-");
          return (zipMs, $"case_{safeName}_documents.zip");
     }
}
