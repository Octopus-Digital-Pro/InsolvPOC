using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Data.Infrastructure;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// Generic file-to-case document upload.
///
/// Unlike the AI classification workflow (<c>POST /api/documents/upload</c>),
/// this endpoint accepts an explicit document type chosen by the user,
/// stores the file under the canonical folder structure
/// <c>cases/{caseId}/{docType}/{docId}{ext}</c>, runs AI field extraction
/// with annotation context, and returns the fully enriched document record.
/// </summary>
[ApiController]
[Route("api/cases/{caseId:guid}/documents")]
[Authorize]
public class CaseDocumentsController : ControllerBase
{
    private readonly ICaseDocumentUploadService _upload;

    public CaseDocumentsController(ICaseDocumentUploadService upload)
        => _upload = upload;

    // ── Upload document to case ──────────────────────────────────────────────

    /// <summary>
    /// Upload any document to an existing case.
    ///
    /// <para><b>docType</b> must be one of the well-known values returned by
    /// <c>GET /api/cases/document-types</c>, or any custom string that will be
    /// stored verbatim and used as the folder name.</para>
    ///
    /// <para>After upload the service:
    /// <list type="number">
    ///   <item>Creates <c>cases/{caseId}/{docType}/{docId}{ext}</c> in storage.</item>
    ///   <item>Saves an InsolvencyDocument DB row.</item>
    ///   <item>Extracts text from the PDF.</item>
    ///   <item>Applies saved IncomingDocumentProfile annotations as AI hints.</item>
    ///   <item>Runs AI field extraction and persists results.</item>
    /// </list></para>
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permission.DocumentUpload)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        Guid caseId,
        [FromQuery] string docType,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (string.IsNullOrWhiteSpace(docType))
            docType = "Other";

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await _upload.UploadAsync(
                caseId,
                docType,
                stream,
                file.FileName,
                file.ContentType,
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Ensure folder structure ──────────────────────────────────────────────

    /// <summary>
    /// Ensures the standard sub-folder structure exists for a case.
    /// Idempotent — safe to call repeatedly.
    /// </summary>
    [HttpPost("ensure-folders")]
    [RequirePermission(Permission.DocumentUpload)]
    public async Task<IActionResult> EnsureFolders(Guid caseId, CancellationToken ct)
    {
        await _upload.EnsureCaseFolderStructureAsync(caseId, ct);
        return Ok(new
        {
            caseId,
            message = "Folder structure ensured.",
            folders = CaseStorageKeys.StandardDocTypes,
        });
    }

    // ── Known document types ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of well-known document types supported by the application.
    /// Callers may also pass any custom string as <c>docType</c>.
    /// </summary>
    [HttpGet("/api/cases/document-types")]
    [RequirePermission(Permission.DocumentView)]
    public IActionResult GetDocumentTypes()
        => Ok(CaseStorageKeys.StandardDocTypes.Select(t => new
        {
            value = t,
            label = ToLabel(t),
        }));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ToLabel(string type) => type switch
    {
        "CourtOpeningDecision" => "Court Opening Decision",
        "BpiPublication"       => "BPI Publication",
        "CreditorNotification" => "Creditor Notification",
        "CreditorClaim"        => "Creditor Claim",
        "AssetInventory"       => "Asset Inventory",
        "PractitionerReport"   => "Practitioner Report",
        "FinancialStatement"   => "Financial Statement",
        "TaxCertificate"       => "Tax Certificate",
        "BankStatement"        => "Bank Statement",
        "LiquidationReport"    => "Liquidation Report",
        "Generated"            => "Generated Document",
        "Other"                => "Other",
        _                      => type,
    };
}
