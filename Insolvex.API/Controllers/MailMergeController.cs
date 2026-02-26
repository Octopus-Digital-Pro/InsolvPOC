using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Data.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.TemplateView)]
public class MailMergeController : ControllerBase
{
    private readonly MailMergeService _mailMerge;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public MailMergeController(MailMergeService mailMerge, IFileStorageService storage, IAuditService audit, ICurrentUserService currentUser)
    {
        _mailMerge = mailMerge;
        _storage = storage;
        _audit = audit;
        _currentUser = currentUser;
    }

    /// <summary>
    /// List all available document templates (disk + DB overrides merged).
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _mailMerge.GetAvailableTemplatesAsync(_currentUser.TenantId);
        return Ok(templates);
    }

    /// <summary>
    /// Generate a specific document from a template for a case.
    /// </summary>
    [HttpPost("generate/{caseId:guid}")]
    [RequirePermission(Permission.TemplateGenerate)]
    public async Task<IActionResult> Generate(Guid caseId, [FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<DocumentTemplateType>(request.TemplateType, true, out var templateType))
            return BadRequest($"Unknown template type: {request.TemplateType}");

        var result = await _mailMerge.GenerateForCaseAsync(caseId, templateType, ct);

        await _audit.LogEntityAsync("Document Generated from Template", "InsolvencyCase", caseId,
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
    public async Task<IActionResult> GenerateAll(Guid caseId, CancellationToken ct, [FromQuery] string? detectedDocType = null)
    {
        var results = await _mailMerge.GenerateKeyDocumentsForCaseIdAsync(caseId, detectedDocType, ct);

        await _audit.LogEntityAsync("Batch Documents Generated from Templates", "InsolvencyCase", caseId,
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
