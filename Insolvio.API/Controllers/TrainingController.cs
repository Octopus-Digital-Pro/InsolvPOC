using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/training")]
[Authorize]
public class TrainingController : ControllerBase
{
    private readonly ITrainingService _training;

    public TrainingController(ITrainingService training) => _training = training;

    /// <summary>
    /// List all uploaded training documents for the current tenant.
    /// </summary>
    [HttpGet("documents")]
    [RequirePermission(Permission.TrainingView)]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _training.GetDocumentsAsync(page, pageSize, ct);
        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>
    /// Upload a new document for annotation and training.
    /// </summary>
    [HttpPost("documents")]
    [RequirePermission(Permission.TrainingManage)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] string documentType, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var allowedExtensions = new[] { ".pdf", ".docx", ".doc" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest("Only PDF, DOCX, and DOC files are accepted.");

        using var stream = file.OpenReadStream();
        var result = await _training.UploadDocumentAsync(documentType, file.FileName, stream, ct);
        return Ok(result);
    }

    /// <summary>
    /// Save field annotations for a training document.
    /// </summary>
    [HttpPut("documents/{id}/annotations")]
    [RequirePermission(Permission.TrainingManage)]
    public async Task<IActionResult> SaveAnnotations(Guid id, [FromBody] AnnotationsPayload payload, CancellationToken ct)
    {
        await _training.SaveAnnotationsAsync(id, payload.AnnotationsJson, ct);
        return NoContent();
    }

    /// <summary>
    /// Mark a training document as approved for model training.
    /// </summary>
    [HttpPost("documents/{id}/approve")]
    [RequirePermission(Permission.TrainingManage)]
    public async Task<IActionResult> ApproveDocument(Guid id, CancellationToken ct)
    {
        await _training.ApproveDocumentAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Get the current training status (document counts, job status).
    /// </summary>
    [HttpGet("status")]
    [RequirePermission(Permission.TrainingView)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _training.GetStatusAsync(ct);
        return Ok(status);
    }

    public record AnnotationsPayload(string AnnotationsJson);
}
