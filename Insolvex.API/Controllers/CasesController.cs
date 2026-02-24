using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.CaseView)]
public class CasesController : ControllerBase
{
    private readonly ICaseService _cases;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CasesController(ICaseService cases, ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _cases = cases;
        _db = db;
        _currentUser = currentUser;
    }

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

    /// <summary>Export all cases for the current tenant to CSV.</summary>
    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
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
            Stage = c.Stage.ToString(),
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

        var bytes = Encoding.UTF8.GetBytes(writer.ToString());
        return File(bytes, "text/csv", $"cases_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>Download all documents for a case as a ZIP archive.</summary>
    [HttpGet("{id:guid}/documents/download-zip")]
    public async Task<IActionResult> DownloadDocumentsZip(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var caseEntity = await _db.InsolvencyCases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && (tenantId == null || c.TenantId == tenantId), ct);
        if (caseEntity is null) return NotFound();

        var docs = await _db.InsolvencyDocuments
            .AsNoTracking()
            .Where(d => d.CaseId == id && !string.IsNullOrEmpty(d.StorageKey))
            .ToListAsync(ct);

        if (docs.Count == 0)
            return NotFound(new { message = "No documents with stored files found for this case." });

        var storage = HttpContext.RequestServices.GetRequiredService<IFileStorageService>();
        var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var doc in docs)
            {
                try
                {
                    var fileStream = await storage.DownloadAsync(doc.StorageKey!, ct);
                    var entryName = $"{doc.DocType}/{doc.SourceFileName}";
                    // Sanitize duplicates by prefixing with short id
                    var entry = archive.CreateEntry(entryName.Replace('/', '_'), CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await fileStream.CopyToAsync(entryStream, ct);
                }
                catch
                {
                    // Skip documents whose storage files are missing
                }
            }
        }

        zipMs.Seek(0, SeekOrigin.Begin);
        var safeName = caseEntity.CaseNumber.Replace("/", "-").Replace("\\", "-");
        return File(zipMs, "application/zip", $"case_{safeName}_documents.zip");
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
  ProcedureType = body.ProcedureType,
       Stage = body.Stage,
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

// ?? API request bodies ??

public record CreateCaseBody(
    string CaseNumber,
 string DebtorName,
    string? CourtName = null,
    string? CourtSection = null,
    string? DebtorCui = null,
    ProcedureType? ProcedureType = null,
    string? LawReference = null,
    Guid? CompanyId = null);

public record UpdateCaseBody(
 string? CaseNumber = null, string? CourtName = null, string? CourtSection = null,
    string? JudgeSyndic = null, ProcedureType? ProcedureType = null, CaseStage? Stage = null,
    string? LawReference = null, string? PractitionerName = null, string? PractitionerRole = null,
    string? PractitionerFiscalId = null, string? PractitionerDecisionNo = null,
    DateTime? NoticeDate = null, DateTime? OpeningDate = null, DateTime? NextHearingDate = null,
    DateTime? ClaimsDeadline = null, DateTime? ContestationsDeadline = null,
    DateTime? DefinitiveTableDate = null, DateTime? ReorganizationPlanDeadline = null, DateTime? ClosureDate = null,
    decimal? TotalClaimsRon = null, decimal? SecuredClaimsRon = null, decimal? UnsecuredClaimsRon = null,
    decimal? BudgetaryClaimsRon = null, decimal? EmployeeClaimsRon = null, decimal? EstimatedAssetValueRon = null,
    string? BpiPublicationNo = null, DateTime? BpiPublicationDate = null, string? OpeningDecisionNo = null,
    string? Notes = null, Guid? CompanyId = null, Guid? AssignedToUserId = null);
