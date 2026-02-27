using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// HTTP adapter for document upload operations.
/// All business logic is delegated to <see cref="IDocumentUploadService"/>.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentUploadController : ControllerBase
{
    private readonly IDocumentUploadService _uploadService;

    public DocumentUploadController(IDocumentUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    /// <summary>
    /// Upload a document for AI classification.
    /// Returns the classification result for user review before confirmation.
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permission.DocumentUpload)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        await using var stream = file.OpenReadStream();

        var result = await _uploadService.ClassifyAndStoreUploadAsync(
   new DocumentUploadRequest
   {
       FileName = file.FileName,
       FileSize = file.Length,
       ContentType = file.ContentType,
       FileStream = stream,
   }, ct);

        return Ok(result);
    }

    /// <summary>
    /// Retrieve a previously uploaded document's AI classification details.
    /// </summary>
    [HttpGet("upload/{id:guid}")]
    public async Task<IActionResult> GetUpload(Guid id, CancellationToken ct)
    {
        var result = await _uploadService.GetPendingUploadAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Confirm a pending upload: create a new insolvency case or file
    /// the document into an existing case.
    /// </summary>
    [HttpPost("upload/{id:guid}/confirm")]
    [RequirePermission(Permission.CaseCreate)]
    public async Task<IActionResult> ConfirmUpload(
        Guid id,
 [FromBody] ConfirmUploadBody body,
        CancellationToken ct)
    {
        var command = new ConfirmUploadCommand
        {
            Action = body.Action,
            CaseNumber = body.CaseNumber,
            CourtName = body.CourtName,
            CourtSection = body.CourtSection,
            DebtorName = body.DebtorName,
            DebtorCui = body.DebtorCui,
            JudgeSyndic = body.JudgeSyndic,
            ProcedureType = body.ProcedureType,
            OpeningDate = body.OpeningDate,
            NextHearingDate = body.NextHearingDate,
            ClaimsDeadline = body.ClaimsDeadline,
            ContestationsDeadline = body.ContestationsDeadline,
            CompanyId = body.CompanyId,
            CaseId = body.CaseId,
            Parties = body.Parties?.Select(p => new ExtractedPartyResult
            {
                Role = p.Role,
                Name = p.Name,
                FiscalId = p.FiscalId,
                ClaimAmount = p.ClaimAmount,
            }).ToList(),
        };

        var result = await _uploadService.ConfirmUploadAsync(id, command, ct);
        return Ok(result);
    }
}

// 📝 Request body (API contract only — no business logic) ────

public record ConfirmUploadBody(
    string Action,
    string? CaseNumber = null,
    string? CourtName = null,
    string? CourtSection = null,
    string? DebtorName = null,
    string? DebtorCui = null,
    string? JudgeSyndic = null,
 string? ProcedureType = null,
    DateTime? OpeningDate = null,
    DateTime? NextHearingDate = null,
    DateTime? ClaimsDeadline = null,
    DateTime? ContestationsDeadline = null,
    Guid? CompanyId = null,
    Guid? CaseId = null,
    List<ConfirmUploadPartyBody>? Parties = null
);

public record ConfirmUploadPartyBody(
    string Role,
    string Name,
string? FiscalId = null,
    decimal? ClaimAmount = null
);
