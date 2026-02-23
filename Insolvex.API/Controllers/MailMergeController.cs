using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.API.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.TemplateView)]
public class MailMergeController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly MailMergeService _mailMerge;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;

    public MailMergeController(ApplicationDbContext db, MailMergeService mailMerge, IFileStorageService storage, IAuditService audit)
    {
        _db = db;
   _mailMerge = mailMerge;
        _storage = storage;
        _audit = audit;
    }

  /// <summary>
    /// List all available document templates.
    /// </summary>
    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
    return Ok(_mailMerge.GetAvailableTemplates());
    }

    /// <summary>
    /// Generate a specific document from a template for a case.
    /// </summary>
    [HttpPost("generate/{caseId:guid}")]
    [RequirePermission(Permission.TemplateGenerate)]
    public async Task<IActionResult> Generate(Guid caseId, [FromBody] GenerateDocumentRequest request)
    {
        var cas = await _db.InsolvencyCases
      .Include(c => c.Company)
.FirstOrDefaultAsync(c => c.Id == caseId);
      if (cas == null) return NotFound("Case not found");

        if (!Enum.TryParse<DocumentTemplateType>(request.TemplateType, true, out var templateType))
        return BadRequest($"Unknown template type: {request.TemplateType}");

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();

   var result = await _mailMerge.GenerateAsync(templateType, cas, cas.Company, firm);

        await _audit.LogEntityAsync("Document.MailMergeGenerated", "InsolvencyCase", caseId,
 newValues: new { templateType = result.TemplateType, result.FileName, result.FileSizeBytes });

        return Ok(new
        {
            result.TemplateType,
   result.StorageKey,
    result.FileName,
  result.FileSizeBytes,
         downloadUrl = _storage.GetPresignedUrl(result.StorageKey, TimeSpan.FromHours(1)),
        });
    }

    /// <summary>
    /// Generate all key documents for a case based on its procedure type.
    /// </summary>
    [HttpPost("generate-all/{caseId:guid}")]
    [RequirePermission(Permission.TemplateGenerate)]
    public async Task<IActionResult> GenerateAll(Guid caseId, [FromQuery] string? detectedDocType = null)
    {
        var cas = await _db.InsolvencyCases
       .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == caseId);
        if (cas == null) return NotFound("Case not found");

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();

var results = await _mailMerge.GenerateKeyDocumentsForCaseAsync(
        cas, cas.Company, firm, detectedDocType);

        await _audit.LogEntityAsync("Document.MailMergeBatchGenerated", "InsolvencyCase", caseId,
 newValues: new { count = results.Count, templates = results.Select(r => r.TemplateType).ToList() });

        return Ok(results.Select(r => new
     {
            r.TemplateType,
       r.StorageKey,
r.FileName,
      r.FileSizeBytes,
            downloadUrl = _storage.GetPresignedUrl(r.StorageKey, TimeSpan.FromHours(1)),
  }));
    }

    /// <summary>
    /// Download a generated document by its storage key.
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string key)
    {
 if (string.IsNullOrWhiteSpace(key))
 return BadRequest("Key is required");

     if (!await _storage.ExistsAsync(key))
         return NotFound("Document not found");

     var stream = await _storage.DownloadAsync(key);
 var fileName = Path.GetFileName(key);
var contentType = Path.GetExtension(fileName) switch
    {
     ".pdf" => "application/pdf",
     ".doc" => "application/msword",
  ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
   };

        return File(stream, contentType, fileName);
    }
}

public record GenerateDocumentRequest(string TemplateType);
