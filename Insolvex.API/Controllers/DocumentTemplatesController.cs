using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.Data;
using Insolvex.Data.Services;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// CRUD for rich-text document templates.
/// System templates (IsSystem = true) are seeded globally and cannot be deleted,
/// but their BodyHtml can be customised per-tenant.
/// Custom templates (IsSystem = false) are fully managed by the tenant.
/// </summary>
[ApiController]
[Route("api/document-templates")]
[Authorize]
public class DocumentTemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly TemplateGenerationService _generator;
    private readonly IFileStorageService _storage;
    private readonly HtmlPdfService _htmlPdf;

    public DocumentTemplatesController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        TemplateGenerationService generator,
        IFileStorageService storage,
        HtmlPdfService htmlPdf)
    {
        _db = db;
        _currentUser = currentUser;
        _generator = generator;
        _storage = storage;
        _htmlPdf = htmlPdf;
    }

    // ── Available placeholder groups (for the editor sidebar) ────────────────

    /// <summary>Returns all placeholder groups and their fields for the editor.</summary>
    [HttpGet("placeholders")]
    [RequirePermission(Permission.TemplateView)]
    public IActionResult GetPlaceholders()
    {
        var groups = new[]
        {
            new
            {
                group = "Dosar",
                fields = new[]
                {
                    new { key = "CaseNumber",          label = "Număr dosar" },
                    new { key = "ProcedureType",        label = "Tip procedură" },
                    new { key = "LawReference",         label = "Temei legal" },
                    new { key = "OpeningDecisionNo",    label = "Nr. sentință deschidere" },
                    new { key = "BpiPublicationNo",     label = "Nr. publicare BPI" },
                    new { key = "BpiPublicationDate",   label = "Dată publicare BPI" },
                }
            },
            new
            {
                group = "Debitor",
                fields = new[]
                {
                    new { key = "DebtorName",           label = "Denumire debitor" },
                    new { key = "DebtorCui",            label = "CUI debitor" },
                    new { key = "DebtorAddress",        label = "Adresă debitor" },
                    new { key = "DebtorLocality",       label = "Localitate debitor" },
                    new { key = "DebtorCounty",         label = "Județ debitor" },
                    new { key = "DebtorTradeRegisterNo",label = "Nr. Reg. Comerțului" },
                    new { key = "DebtorCaen",           label = "Cod CAEN" },
                    new { key = "DebtorAdministratorName", label = "Reprezentant legal" },
                }
            },
            new
            {
                group = "Instanță",
                fields = new[]
                {
                    new { key = "CourtName",            label = "Instanță" },
                    new { key = "CourtSection",         label = "Secție" },
                    new { key = "JudgeSyndic",          label = "Judecător sindic (dosar)" },
                    new { key = "JudgeName",            label = "Judecător (registratură)" },
                    new { key = "CourtRegistryAddress", label = "Adresă registratură" },
                    new { key = "CourtRegistryPhone",   label = "Telefon registratură" },
                    new { key = "CourtRegistryHours",   label = "Program registratură" },
                }
            },
            new
            {
                group = "Practicant / Firmă",
                fields = new[]
                {
                    new { key = "PractitionerName",     label = "Practicant insolvenţă" },
                    new { key = "PractitionerRole",     label = "Calitate practicant" },
                    new { key = "PractitionerEntityName", label = "Entitate practicant" },
                    new { key = "PractitionerCUI",      label = "CUI practicant" },
                    new { key = "PractitionerAddress",  label = "Adresă practicant" },
                    new { key = "PractitionerUNPIRNo",  label = "Nr. UNPIR" },
                    new { key = "PractitionerPhone",    label = "Telefon practicant" },
                    new { key = "PractitionerFax",      label = "Fax practicant" },
                    new { key = "PractitionerEmail",    label = "Email practicant" },
                    new { key = "PractitionerDecisionNo", label = "Nr. decizie numire" },
                    new { key = "PractitionerRepresentativeName", label = "Reprezentant practicant" },
                    new { key = "FirmName",             label = "Firmă insolvență" },
                    new { key = "FirmCui",              label = "CUI firmă" },
                    new { key = "FirmAddress",          label = "Adresă firmă" },
                    new { key = "FirmPhone",            label = "Telefon firmă" },
                    new { key = "FirmEmail",            label = "Email firmă" },
                    new { key = "FirmIban",             label = "IBAN firmă" },
                    new { key = "FirmBankName",         label = "Bancă firmă" },
                }
            },
            new
            {
                group = "Date cheie",
                fields = new[]
                {
                    new { key = "NoticeDate",               label = "Dată notificare" },
                    new { key = "OpeningDate",              label = "Dată deschidere procedură" },
                    new { key = "ClaimsDeadline",           label = "Termen depunere cereri de creanță" },
                    new { key = "ContestationsDeadline",    label = "Termen contestații" },
                    new { key = "NextHearingDate",          label = "Următor termen instanță" },
                    new { key = "CurrentDate",              label = "Data curentă" },
                    new { key = "CurrentYear",              label = "Anul curent" },
                }
            },
            new
            {
                group = "Adunarea creditorilor",
                fields = new[]
                {
                    new { key = "CreditorsMeetingDate",     label = "Dată adunare creditori" },
                    new { key = "CreditorsMeetingTime",     label = "Ora adunare creditori" },
                    new { key = "CreditorsMeetingAddress",  label = "Locație adunare creditori" },
                }
            },
            new
            {
                group = "Financiar",
                fields = new[]
                {
                    new { key = "TotalClaimsRon",           label = "Total creanțe (RON)" },
                    new { key = "SecuredClaimsRon",         label = "Creanțe garantate (RON)" },
                    new { key = "UnsecuredClaimsRon",       label = "Creanțe negarantate (RON)" },
                }
            },
            new
            {
                group = "Destinatar (per-parte)",
                fields = new[]
                {
                    new { key = "RecipientName",            label = "Denumire destinatar" },
                    new { key = "RecipientAddress",         label = "Adresă destinatar" },
                    new { key = "RecipientEmail",           label = "Email destinatar" },
                    new { key = "RecipientIdentifier",      label = "CUI/CNP destinatar" },
                    new { key = "RecipientRole",            label = "Rol destinatar" },
                }
            },
            // ── Collection groups ({{#each}}) ────────────────────────────────
            new
            {
                group = "🔁 Creditori ({{#each Creditors}})",
                fields = new[]
                {
                    new { key = "RowNo",        label = "Nr. crt." },
                    new { key = "Name",         label = "Denumire creditor" },
                    new { key = "Role",         label = "Rol / Tip creditor" },
                    new { key = "Identifier",   label = "CUI / CNP" },
                    new { key = "Address",      label = "Adresă creditor" },
                    new { key = "Email",        label = "Email creditor" },
                }
            },
            new
            {
                group = "🔁 Creanțe ({{#each Claims}})",
                fields = new[]
                {
                    new { key = "RowNo",            label = "Nr. crt." },
                    new { key = "CreditorName",     label = "Creditor" },
                    new { key = "DeclaredAmount",   label = "Sumă declarată" },
                    new { key = "AdmittedAmount",   label = "Sumă admisă" },
                    new { key = "Percent",          label = "Procent din total" },
                    new { key = "Rank",             label = "Rang / Prioritate" },
                    new { key = "Status",           label = "Status creanță" },
                }
            },
            new
            {
                group = "🔁 Active ({{#each Assets}})",
                fields = new[]
                {
                    new { key = "RowNo",            label = "Nr. crt." },
                    new { key = "Description",      label = "Descriere activ" },
                    new { key = "Type",             label = "Tip activ" },
                    new { key = "EstimatedValue",   label = "Valoare estimată" },
                    new { key = "Status",           label = "Status activ" },
                    new { key = "SaleProceeds",     label = "Preț vânzare" },
                }
            },
            // ── Totaluri agregate ─────────────────────────────────────────────
            new
            {
                group = "Totaluri",
                fields = new[]
                {
                    new { key = "Totals.DeclaredTotal",   label = "Total creanțe declarate" },
                    new { key = "Totals.AdmittedTotal",   label = "Total creanțe admise" },
                    new { key = "Totals.CreditorCount",   label = "Număr creditori" },
                    new { key = "Totals.ClaimCount",      label = "Număr creanțe" },
                    new { key = "Totals.AssetCount",      label = "Număr active" },
                }
            },
            // ── Conditional blocks ({{#if}}) ─────────────────────────────────
            new
            {
                group = "❓ Condiții ({{#if}})",
                fields = new[]
                {
                    new { key = "HasCreditors",   label = "Are creditori?" },
                    new { key = "HasClaims",      label = "Are creanțe?" },
                    new { key = "HasAssets",       label = "Are active?" },
                }
            },
            // ── Electronic signature ─────────────────────────────────────────
            new
            {
                group = "✍ Semnătură electronică",
                fields = new[]
                {
                    new { key = "ElectronicSignature", label = "Bloc semnătură electronică" },
                }
            },
        };

        return Ok(groups);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all templates visible to the current tenant:
    /// system templates (IsSystem=true, TenantId=null) PLUS
    /// tenant-owned custom templates.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var templates = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .Where(t => t.IsSystem || t.TenantId == tenantId)
            .OrderBy(t => t.IsSystem ? 0 : 1)
            .ThenBy(t => t.TemplateType)
            .ThenBy(t => t.Name)
            .Select(t => new DocumentTemplateDto(
                t.Id,
                t.Name,
                t.Description,
                t.TemplateType,
                t.IsSystem,
                t.IsActive,
                t.Stage,
                t.Category,
                t.BodyHtml != null && t.BodyHtml.Length > 0,
                t.CreatedOn,
                t.LastModifiedOn))
            .ToListAsync(ct);

        return Ok(templates);
    }

    // ── Get single (with BodyHtml) ────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && (t.IsSystem || t.TenantId == tenantId), ct);

        if (t == null) return NotFound();

        return Ok(new DocumentTemplateDetailDto(
            t.Id, t.Name, t.Description, t.TemplateType,
            t.IsSystem, t.IsActive, t.Stage, t.Category, t.BodyHtml,
            t.CreatedOn, t.LastModifiedOn));
    }

    // ── Create custom template ────────────────────────────────────────────────

    [HttpPost]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentTemplateRequest req, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new BusinessException("No tenant context");

        var template = new DocumentTemplate
        {
            TenantId = tenantId,
            TemplateType = DocumentTemplateType.Custom,
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            Category = req.Category?.Trim(),
            BodyHtml = req.BodyHtml,
            IsSystem = false,
            IsActive = true,
            // File fields unused for HTML templates
            FileName = "",
            StorageKey = "",
            ContentType = "text/html",
        };

        _db.DocumentTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = template.Id },
            new DocumentTemplateDto(template.Id, template.Name, template.Description,
                template.TemplateType, template.IsSystem, template.IsActive,
                template.Stage, template.Category,
                template.BodyHtml != null && template.BodyHtml.Length > 0,
                template.CreatedOn, template.LastModifiedOn));
    }

    // ── Update template ───────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentTemplateRequest req, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && (x.IsSystem || x.TenantId == tenantId), ct);

        if (t == null) return NotFound();

        // System templates: only BodyHtml and IsActive can be changed
        t.BodyHtml = req.BodyHtml;
        t.IsActive = req.IsActive;

        if (!t.IsSystem)
        {
            t.Name = req.Name.Trim();
            t.Description = req.Description?.Trim();
            t.Category = req.Category?.Trim();
        }

        t.Version++;
        await _db.SaveChangesAsync(ct);

        return Ok(new DocumentTemplateDetailDto(
            t.Id, t.Name, t.Description, t.TemplateType,
            t.IsSystem, t.IsActive, t.Stage, t.Category, t.BodyHtml,
            t.CreatedOn, t.LastModifiedOn));
    }

    // ── Delete custom template ────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);

        if (t == null) return NotFound();
        if (t.IsSystem) return BadRequest(new { message = "System templates cannot be deleted." });

        _db.DocumentTemplates.Remove(t);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Deleted" });
    }

    // ── Render template with real case data ───────────────────────────────────

    [HttpPost("{id:guid}/render")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> Render(
        Guid id,
        [FromBody] RenderTemplateRequest req,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && (x.IsSystem || x.TenantId == tenantId), ct);

        if (t == null) return NotFound();
        if (string.IsNullOrWhiteSpace(t.BodyHtml))
            return BadRequest(new { message = "Template has no HTML body content." });

        var (renderedHtml, mergeData) = await _generator.RenderHtmlBodyAsync(
            t.BodyHtml, req.CaseId, req.RecipientPartyId);

        return Ok(new RenderTemplateResult(renderedHtml, mergeData));
    }

    // ── Render template to PDF and return directly ───────────────────────────

    /// <summary>
    /// Renders the template's HTML body against a real case and returns a
    /// ready-to-download PDF file using PuppeteerSharp (headless Chromium).
    /// </summary>
    [HttpPost("{id:guid}/render-pdf")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> RenderPdf(
        Guid id,
        [FromBody] RenderTemplateRequest req,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && (x.IsSystem || x.TenantId == tenantId), ct);

        if (t == null) return NotFound();
        if (string.IsNullOrWhiteSpace(t.BodyHtml))
            return BadRequest(new { message = "Template has no HTML body content." });

        var (renderedHtml, _) = await _generator.RenderHtmlBodyAsync(t.BodyHtml, req.CaseId, req.RecipientPartyId);
        var pdfBytes = await _htmlPdf.RenderHtmlStringToPdfBytesAsync(renderedHtml, ct);
        var safeName = string.Concat(t.Name.Split(Path.GetInvalidFileNameChars()))
                           .Replace(" ", "_");
        var fileName = $"{safeName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // ── Save rendered PDF directly into the case Documents tab ───────────────

    /// <summary>
    /// Renders the template to PDF and saves it as an InsolvencyDocument on the case.
    /// The saved document is flagged RequiresSignature = true so it appears in the
    /// signing queue immediately.
    /// </summary>
    [HttpPost("{id:guid}/save-to-case")]
    [RequirePermission(Permission.TemplateGenerate)]
    public async Task<IActionResult> SaveToCase(
        Guid id,
        [FromBody] RenderTemplateRequest req,
        CancellationToken ct)
    {
        if (_currentUser.TenantId is null)
            return BadRequest(new { message = "No tenant context." });
        var tenantId = _currentUser.TenantId.Value;

        var t = await _db.DocumentTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && (x.IsSystem || x.TenantId == tenantId), ct);

        if (t == null) return NotFound();
        if (string.IsNullOrWhiteSpace(t.BodyHtml))
            return BadRequest(new { message = "Template has no HTML body content." });

        // 1. Render HTML then convert to PDF
        var (renderedHtml, _) = await _generator.RenderHtmlBodyAsync(t.BodyHtml, req.CaseId, req.RecipientPartyId);
        var pdfBytes = await _htmlPdf.RenderHtmlStringToPdfBytesAsync(renderedHtml, ct);

        // 2. Persist the PDF in file storage
        var docId = Guid.NewGuid();
        var safeName = string.Concat(t.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var fileName = $"{safeName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        var storageKey = $"cases/{req.CaseId}/generated/{docId}.pdf";

        using var ms = new MemoryStream(pdfBytes);
        await _storage.UploadAsync(storageKey, ms, "application/pdf", ct);

        // 3. Create the InsolvencyDocument record
        var doc = new InsolvencyDocument
        {
            Id = docId,
            TenantId = tenantId,
            CaseId = req.CaseId,
            SourceFileName = fileName,
            StorageKey = storageKey,
            DocType = "GeneratedTemplate",
            Purpose = "Generated",
            RequiresSignature = true,
            UploadedBy = _currentUser.Email ?? "System",
            UploadedAt = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
            Summary = $"Generated from template: {t.Name}",
        };

        _db.InsolvencyDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return Ok(new { documentId = docId, fileName, storageKey, requiresSignature = true });
    }

    // ── Render raw HTML to PDF (for user-edited content from preview modal) ────

    /// <summary>
    /// Converts arbitrary HTML content to PDF and returns the file for download.
    /// Used when the user has edited the rendered template in the preview modal.
    /// </summary>
    [HttpPost("render-html-to-pdf")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> RenderHtmlToPdf(
        [FromBody] RenderHtmlToPdfRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Html))
            return BadRequest(new { message = "HTML content is required." });

        var pdfBytes = await _htmlPdf.RenderHtmlStringToPdfBytesAsync(req.Html, ct);
        var safeName = string.Concat(
            (req.TemplateName ?? "document").Split(Path.GetInvalidFileNameChars()))
            .Replace(" ", "_");
        var fileName = $"{safeName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // ── Save arbitrary HTML to case documents ────────────────────────────────

    /// <summary>
    /// Converts arbitrary HTML to PDF and saves it as an InsolvencyDocument on the case.
    /// Used after the user reviews/edits and optionally signs the rendered template.
    /// </summary>
    [HttpPost("save-html-to-case")]
    [RequirePermission(Permission.TemplateGenerate)]
    public async Task<IActionResult> SaveHtmlToCase(
        [FromBody] SaveHtmlToCaseRequest req,
        CancellationToken ct)
    {
        if (_currentUser.TenantId is null)
            return BadRequest(new { message = "No tenant context." });
        var tenantId = _currentUser.TenantId.Value;

        if (string.IsNullOrWhiteSpace(req.Html))
            return BadRequest(new { message = "HTML content is required." });

        var pdfBytes = await _htmlPdf.RenderHtmlStringToPdfBytesAsync(req.Html, ct);

        var docId = Guid.NewGuid();
        var safeName = string.Concat(
            (req.TemplateName ?? "document").Split(Path.GetInvalidFileNameChars()))
            .Replace(" ", "_");
        var fileName = $"{safeName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        var storageKey = $"cases/{req.CaseId}/generated/{docId}.pdf";

        using var ms = new MemoryStream(pdfBytes);
        await _storage.UploadAsync(storageKey, ms, "application/pdf", ct);

        var doc = new InsolvencyDocument
        {
            Id = docId,
            TenantId = tenantId,
            CaseId = req.CaseId,
            SourceFileName = fileName,
            StorageKey = storageKey,
            DocType = "GeneratedTemplate",
            Purpose = "Generated",
            RequiresSignature = false, // already signed / reviewed
            UploadedBy = _currentUser.Email ?? "System",
            UploadedAt = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? "System",
            Summary = $"Generated from template: {req.TemplateName}",
        };

        _db.InsolvencyDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return Ok(new { documentId = docId, fileName, storageKey, requiresSignature = false });
    }

    // ── Incoming document reference samples (for AI recognition) ─────────────

    /// <summary>
    /// Upload a sample PDF for an incoming document type.
    /// The system stores it as a reference so AI can recognise similar documents
    /// uploaded by practitioners and auto-classify them.
    /// </summary>
    [HttpPost("incoming-reference/{type}")]
    [RequirePermission(Permission.TemplateManage)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadIncomingReference(
        string type, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf" && file.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == false)
            return BadRequest(new { message = "Only PDF files are accepted." });

        var key = $"incoming-reference/{type}.pdf";
        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(key, stream, "application/pdf", ct);

        return Ok(new
        {
            type,
            fileName = file.FileName,
            fileSize = file.Length,
            uploadedOn = DateTime.UtcNow,
            message = "Reference document uploaded. AI recognition is now active for this document type.",
        });
    }

    /// <summary>Returns whether a reference sample PDF has been uploaded for the given incoming document type.</summary>
    [HttpGet("incoming-reference/{type}")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetIncomingReference(string type, CancellationToken ct)
    {
        var key = $"incoming-reference/{type}.pdf";
        try
        {
            using var stream = await _storage.DownloadAsync(key, ct);
            return Ok(new { type, exists = true });
        }
        catch
        {
            return Ok(new { type, exists = false });
        }
    }
}
// ── Additional request DTOs ───────────────────────────────────────────────────

/// <summary>Render arbitrary HTML content to a PDF download.</summary>
public record RenderHtmlToPdfRequest(string Html, string? TemplateName);

/// <summary>Save arbitrary HTML (already rendered and optionally signed) as a case document.</summary>
public record SaveHtmlToCaseRequest(string Html, Guid CaseId, string TemplateName);