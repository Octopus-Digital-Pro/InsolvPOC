using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvio.API.Authorization;
using Insolvio.Data;
using Insolvio.Core.Services;
using Insolvio.Integrations.Services;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Exceptions;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

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
    private readonly IHtmlPdfService _htmlPdf;
    private readonly WordTemplateImportService _wordImport;
    private readonly IncomingDocumentProfileService _incomingProfiles;
    private readonly IDocumentAiService _documentAi;

    public DocumentTemplatesController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        TemplateGenerationService generator,
        IFileStorageService storage,
        IHtmlPdfService htmlPdf,
        WordTemplateImportService wordImport,
        IncomingDocumentProfileService incomingProfiles,
        IDocumentAiService documentAi)
    {
        _db = db;
        _currentUser = currentUser;
        _generator = generator;
        _storage = storage;
        _htmlPdf = htmlPdf;
        _wordImport = wordImport;
        _incomingProfiles = incomingProfiles;
        _documentAi = documentAi;
    }

    // ── Word document import ──────────────────────────────────────────────────

    /// <summary>
    /// Accepts a .docx Word document, extracts its content as HTML, and uses the
    /// configured AI model to detect and insert {{PlaceholderKey}} tokens.
    /// Returns the processed HTML and the list of detected placeholder keys.
    /// </summary>
    [HttpPost("import-word")]
    [RequirePermission(Permission.TemplateManage)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> ImportWordDocument(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".docx")
            return BadRequest(new { message = "Only .docx Word documents are accepted." });

        await using var stream = file.OpenReadStream();

        var groups = BuildPlaceholderGroups();

        try
        {
            var result = await _wordImport.ImportAsync(stream, groups, ct);
            return Ok(new
            {
                html = result.Html,
                detectedPlaceholders = result.DetectedPlaceholders,
                fileName = file.FileName,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Available placeholder groups (for the editor sidebar) ────────────────

    /// <summary>Returns all placeholder groups and their fields for the editor.</summary>
    [HttpGet("placeholders")]
    [RequirePermission(Permission.TemplateView)]
    public IActionResult GetPlaceholders()
    {
        var groups = BuildPlaceholderGroups();
        return Ok(groups);
    }

    /// <summary>Builds the full list of placeholder groups shared across endpoints.</summary>
    private static WordTemplateImportService.PlaceholderGroupInfo[] BuildPlaceholderGroups()
    {
        // Local helpers to keep the list concise and avoid runtime dynamic binding.
        static WordTemplateImportService.PlaceholderGroupInfo G(
            string group, params WordTemplateImportService.PlaceholderFieldInfo[] fields)
            => new(group, fields);
        static WordTemplateImportService.PlaceholderFieldInfo F(string key, string label)
            => new(key, label);

        return
        [
            G("Dosar",
                F("CaseNumber",           "Număr dosar"),
                F("ProcedureType",         "Tip procedură"),
                F("LawReference",          "Temei legal"),
                F("OpeningDecisionNo",     "Nr. sentință deschidere"),
                F("BpiPublicationNo",      "Nr. publicare BPI"),
                F("BpiPublicationDate",    "Dată publicare BPI"),
                F("ReportSummary",         "Rezumat raport task-uri")
            ),
            G("Debitor",
                F("DebtorName",            "Denumire debitor"),
                F("DebtorCui",             "CUI debitor"),
                F("DebtorAddress",         "Adresă debitor"),
                F("DebtorLocality",        "Localitate debitor"),
                F("DebtorCounty",          "Județ debitor"),
                F("DebtorTradeRegisterNo", "Nr. Reg. Comerțului"),
                F("DebtorCaen",            "Cod CAEN"),
                F("DebtorAdministratorName", "Reprezentant legal")
            ),
            G("Instanță",
                F("CourtName",             "Instanță"),
                F("CourtSection",          "Secție"),
                F("JudgeSyndic",           "Judecător sindic (dosar)"),
                F("JudgeName",             "Judecător (registratură)"),
                F("CourtRegistryAddress",  "Adresă registratură"),
                F("CourtRegistryPhone",    "Telefon registratură"),
                F("CourtRegistryHours",    "Program registratură")
            ),
            G("Practicant / Firmă",
                F("PractitionerName",      "Practicant insolvenţă"),
                F("PractitionerRole",      "Calitate practicant"),
                F("PractitionerEntityName","Entitate practicant"),
                F("PractitionerCUI",       "CUI practicant"),
                F("PractitionerAddress",   "Adresă practicant"),
                F("PractitionerUNPIRNo",   "Nr. UNPIR"),
                F("PractitionerPhone",     "Telefon practicant"),
                F("PractitionerFax",       "Fax practicant"),
                F("PractitionerEmail",     "Email practicant"),
                F("PractitionerDecisionNo","Nr. decizie numire"),
                F("PractitionerRepresentativeName", "Reprezentant practicant"),
                F("FirmName",              "Firmă insolvență"),
                F("FirmCui",               "CUI firmă"),
                F("FirmAddress",           "Adresă firmă"),
                F("FirmPhone",             "Telefon firmă"),
                F("FirmEmail",             "Email firmă"),
                F("FirmIban",              "IBAN firmă"),
                F("FirmBankName",          "Bancă firmă")
            ),
            G("Date cheie",
                F("NoticeDate",            "Dată notificare"),
                F("OpeningDate",           "Dată deschidere procedură"),
                F("ClaimsDeadline",        "Termen depunere cereri de creanță"),
                F("ContestationsDeadline", "Termen contestații"),
                F("NextHearingDate",       "Următor termen instanță"),
                F("CurrentDate",           "Data curentă"),
                F("CurrentYear",           "Anul curent")
            ),
            G("Adunarea creditorilor",
                F("CreditorsMeetingDate",   "Dată adunare creditori"),
                F("CreditorsMeetingTime",   "Ora adunare creditori"),
                F("CreditorsMeetingAddress","Locație adunare creditori")
            ),
            G("Financiar",
                F("TotalClaimsRon",         "Total creanțe (RON)"),
                F("SecuredClaimsRon",       "Creanțe garantate (RON)"),
                F("UnsecuredClaimsRon",     "Creanțe negarantate (RON)")
            ),
            G("Destinatar (per-parte)",
                F("RecipientName",          "Denumire destinatar"),
                F("RecipientAddress",       "Adresă destinatar"),
                F("RecipientEmail",         "Email destinatar"),
                F("RecipientIdentifier",    "CUI/CNP destinatar"),
                F("RecipientRole",          "Rol destinatar")
            ),
            G("Raport periodic (30 zile)",
                F("PastTasksFromDate",           "Început perioadă task-uri trecute"),
                F("PastTasksToDate",             "Sfârșit perioadă task-uri trecute"),
                F("FutureTasksFromDate",         "Început perioadă task-uri viitoare"),
                F("FutureTasksToDate",           "Sfârșit perioadă task-uri viitoare"),
                F("PastTasksSummaryWithReport",  "Task-uri trecute cu rezumat (text)"),
                F("PastTasksSummaryWithReportHtml", "Task-uri trecute cu rezumat (HTML listă)"),
                F("FutureTasksNames",            "Task-uri viitoare (nume, text)"),
                F("FutureTasksNamesHtml",        "Task-uri viitoare (nume, HTML listă)")
            ),
            // ── Collection groups ({{#each}}) ────────────────────────────────
            G("🔁 Creditori ({{#each Creditors}})",
                F("RowNo",       "Nr. crt."),
                F("Name",        "Denumire creditor"),
                F("Role",        "Rol / Tip creditor"),
                F("Identifier",  "CUI / CNP"),
                F("Address",     "Adresă creditor"),
                F("Email",       "Email creditor")
            ),
            G("🔁 Creanțe ({{#each Claims}})",
                F("RowNo",           "Nr. crt."),
                F("CreditorName",    "Creditor"),
                F("DeclaredAmount",  "Sumă declarată"),
                F("AdmittedAmount",  "Sumă admisă"),
                F("Percent",         "Procent din total"),
                F("Rank",            "Rang / Prioritate"),
                F("Status",          "Status creanță")
            ),
            G("🔁 Active ({{#each Assets}})",
                F("RowNo",           "Nr. crt."),
                F("Description",     "Descriere activ"),
                F("Type",            "Tip activ"),
                F("EstimatedValue",  "Valoare estimată"),
                F("Status",          "Status activ"),
                F("SaleProceeds",    "Preț vânzare")
            ),
            // ── Totaluri agregate ─────────────────────────────────────────────
            G("Totaluri",
                F("Totals.DeclaredTotal",  "Total creanțe declarate"),
                F("Totals.AdmittedTotal",  "Total creanțe admise"),
                F("Totals.CreditorCount",  "Număr creditori"),
                F("Totals.ClaimCount",     "Număr creanțe"),
                F("Totals.AssetCount",     "Număr active")
            ),
            // ── Conditional blocks ({{#if}}) ─────────────────────────────────
            G("❓ Condiții ({{#if}})",
                F("HasCreditors",  "Are creditori?"),
                F("HasClaims",     "Are creanțe?"),
                F("HasAssets",     "Are active?")
            ),
            // ── Electronic signature ─────────────────────────────────────────
            G("✍ Semnătură electronică",
                F("ElectronicSignature", "Bloc semnătură electronică")
            ),
        ];
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
            t.BodyHtml,
            req.CaseId,
            req.RecipientPartyId,
            req.PastTasksFromDate,
            req.PastTasksToDate,
            req.FutureTasksFromDate,
            req.FutureTasksToDate);

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

        var (renderedHtml, _) = await _generator.RenderHtmlBodyAsync(
            t.BodyHtml,
            req.CaseId,
            req.RecipientPartyId,
            req.PastTasksFromDate,
            req.PastTasksToDate,
            req.FutureTasksFromDate,
            req.FutureTasksToDate);
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
        var (renderedHtml, _) = await _generator.RenderHtmlBodyAsync(
            t.BodyHtml,
            req.CaseId,
            req.RecipientPartyId,
            req.PastTasksFromDate,
            req.PastTasksToDate,
            req.FutureTasksFromDate,
            req.FutureTasksToDate);
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

        // Persist to DB (creates or updates the profile row)
        await _incomingProfiles.UpsertOnUploadAsync(type, key, file.FileName, file.Length, ct);

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
        // Check DB profile first (most reliable)
        var profile = await _incomingProfiles.GetProfileAsync(type, ct);
        if (profile is not null)
            return Ok(new { type, exists = true });

        // Fall back to storage check for backwards compatibility (before DB was added)
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

    /// <summary>
    /// Streams the previously uploaded reference PDF back to the browser so the
    /// annotation tool can render it with PDF.js without needing a signed storage URL.
    /// </summary>
    [HttpGet("incoming-reference/{type}/file")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetIncomingReferenceFile(string type, CancellationToken ct)
    {
        var key = $"incoming-reference/{type}.pdf";
        try
        {
            var stream = await _storage.DownloadAsync(key, ct);
            return File(stream, "application/pdf", $"{type}-reference.pdf");
        }
        catch
        {
            return NotFound(new { message = "No reference PDF uploaded for this type." });
        }
    }

    /// <summary>Returns saved annotation JSON for a given incoming document type, or an empty list.</summary>
    [HttpGet("incoming-reference/{type}/annotations")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetIncomingAnnotations(string type, CancellationToken ct)
    {
        // Try DB first
        try
        {
            var profile = await _incomingProfiles.GetProfileAsync(type, ct);
            if (profile?.AnnotationsJson is not null)
            {
                IncomingAnnotationItem[] annotations;
                try
                {
                    // Use case-insensitive deserialization to handle both old PascalCase
                    // and new camelCase stored JSON.
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    annotations = System.Text.Json.JsonSerializer.Deserialize<IncomingAnnotationItem[]>(profile.AnnotationsJson, opts)
                                  ?? Array.Empty<IncomingAnnotationItem>();
                }
                catch
                {
                    // Malformed or legacy JSON — return empty rather than 500
                    annotations = Array.Empty<IncomingAnnotationItem>();
                }
                return Ok(new { annotations, notes = profile.AnnotationNotes });
            }
        }
        catch
        {
            // DB unavailable — fall through to storage
        }

        // Fall back to storage JSON file (legacy path)
        var key = $"incoming-reference/{type}.annotations.json";
        try
        {
            using var stream = await _storage.DownloadAsync(key, ct);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(ct);
            return Content(json, "application/json");
        }
        catch
        {
            return Ok(new { annotations = Array.Empty<object>(), notes = (string?)null });
        }
    }

    /// <summary>Persists annotation JSON (rectangle regions with field labels) for an incoming document type.</summary>
    [HttpPost("incoming-reference/{type}/annotations")]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> SaveIncomingAnnotations(
        string type, [FromBody] SaveIncomingAnnotationsRequest req, CancellationToken ct)
    {
        // Persist to storage (backup / legacy consumers)
        var storageKey = $"incoming-reference/{type}.annotations.json";
        var json = System.Text.Json.JsonSerializer.Serialize(req);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream(bytes);
        await _storage.UploadAsync(storageKey, ms, "application/json", ct);

        // Persist annotations array to DB — serialize with camelCase so the GET endpoint
        // returns keys that match the TypeScript IncomingAnnotationItem interface.
        var camelCase = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var annotationsJson = System.Text.Json.JsonSerializer.Serialize(req.Annotations, camelCase);
        await _incomingProfiles.SaveAnnotationsAsync(type, annotationsJson, req.Notes, ct);

        return Ok(new { message = "Annotations saved." });
    }

    /// <summary>
    /// Returns the full DB profile for a given incoming document type, including
    /// AI summaries (EN/RO/HU) and structured field parameters.
    /// </summary>
    [HttpGet("incoming-reference/{type}/profile")]
    [RequirePermission(Permission.TemplateView)]
    public async Task<IActionResult> GetIncomingDocumentProfile(string type, CancellationToken ct)
    {
        var profile = await _incomingProfiles.GetProfileAsync(type, ct);
        if (profile is null)
            return Ok(new { type, exists = false });

        return Ok(new
        {
            type,
            exists = true,
            originalFileName = profile.OriginalFileName,
            fileSizeBytes = profile.FileSizeBytes,
            uploadedOn = profile.UploadedOn,
            lastAnnotatedOn = profile.LastAnnotatedOn,
            annotationCount = CountAnnotations(profile.AnnotationsJson),
            annotationNotes = profile.AnnotationNotes,
            aiSummaryEn = profile.AiSummaryEn,
            aiSummaryRo = profile.AiSummaryRo,
            aiSummaryHu = profile.AiSummaryHu,
            aiParametersJson = profile.AiParametersJson,
            aiModel = profile.AiModel,
            aiConfidence = profile.AiConfidence,
            aiAnalysedOn = profile.AiAnalysedOn,
        });
    }

    /// <summary>
    /// Triggers AI analysis of the reference document type. Generates natural-language
    /// summaries in EN, RO, and HU plus structured field parameters; saves to DB.
    /// </summary>
    [HttpPost("incoming-reference/{type}/analyse")]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> AnalyseIncomingDocument(string type, CancellationToken ct)
    {
        try
        {
            var profile = await _incomingProfiles.AnalyseAsync(type, ct);
            if (profile is null)
                return NotFound(new { message = $"No profile found for document type '{type}'.", type });

            return Ok(new
            {
                type,
                aiSummaryEn = profile.AiSummaryEn,
                aiSummaryRo = profile.AiSummaryRo,
                aiSummaryHu = profile.AiSummaryHu,
                aiParametersJson = profile.AiParametersJson,
                aiModel = profile.AiModel,
                aiConfidence = profile.AiConfidence,
                aiAnalysedOn = profile.AiAnalysedOn,
                message = "AI analysis complete.",
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Ask AI to locate verbatim text for each annotatable field within the supplied
    /// extracted document text. Returns a map of fieldName → exact verbatim substring.
    /// </summary>
    [HttpPost("incoming-reference/{type}/suggest-annotations")]
    [RequirePermission(Permission.TemplateManage)]
    public async Task<IActionResult> SuggestAnnotations(
        string type, [FromBody] SuggestAnnotationsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ExtractedText))
            return BadRequest(new { message = "ExtractedText is required." });

        var suggestions = await _documentAi.SuggestAnnotationsAsync(req.ExtractedText, ct);
        if (suggestions is null)
            return Ok(new { suggestions = new Dictionary<string, string>(), aiConfigured = false });

        return Ok(new { suggestions, aiConfigured = true });
    }

    private static int CountAnnotations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch { return 0; }
    }
}
// ── Additional request DTOs ───────────────────────────────────────────────────

/// <summary>Render arbitrary HTML content to a PDF download.</summary>
public record RenderHtmlToPdfRequest(string Html, string? TemplateName);

/// <summary>Save arbitrary HTML (already rendered and optionally signed) as a case document.</summary>
public record SaveHtmlToCaseRequest(string Html, Guid CaseId, string TemplateName);

/// <summary>A single text-selection annotation on the reference document.</summary>
public record IncomingAnnotationItem(
    string Id,
    string Field,
    string Label,
    string SelectedText,
    string ContextBefore,
    string ContextAfter);

/// <summary>Full set of annotations for one incoming document type reference.</summary>
public record SaveIncomingAnnotationsRequest(
    IReadOnlyList<IncomingAnnotationItem> Annotations,
    string? Notes);

/// <summary>Request body for the AI annotation suggestion endpoint.</summary>
public record SuggestAnnotationsRequest(string ExtractedText);