using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using Insolvio.Core.Abstractions;
using Insolvio.Core.Infrastructure;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

/// <summary>
/// Handles direct uploads of a typed document to an existing case.
///
/// Flow:
///   1. Validate the case exists for the current tenant.
///   2. Ensure the <c>cases/{caseId}/{docType}/</c> folder exists in storage.
///   3. Persist the file to <c>cases/{caseId}/{docType}/{docId}{ext}</c>.
///   4. Create an <see cref="InsolvencyDocument"/> record in the DB.
///   5. Extract text (if PDF).
///   6. Fetch the <see cref="IncomingDocumentProfile"/> for this docType and
///      convert its saved annotations into an AI prompt context hint.
///   7. Send extracted text + annotation context to the AI for field extraction.
///   8. Persist AI results on the InsolvencyDocument record.
/// </summary>
public sealed class CaseDocumentUploadService : ICaseDocumentUploadService
{
    private static readonly string[] AllowedExtensions =
        [".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".tiff"];

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IFileStorageService _storage;
    private readonly IncomingDocumentProfileService _profiles;
    private readonly AiDocumentAnalysisService _aiAnalysis;
    private readonly ILogger<CaseDocumentUploadService> _logger;

    public CaseDocumentUploadService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        IFileStorageService storage,
        IncomingDocumentProfileService profiles,
        AiDocumentAnalysisService aiAnalysis,
        ILogger<CaseDocumentUploadService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _storage = storage;
        _profiles = profiles;
        _aiAnalysis = aiAnalysis;
        _logger = logger;
    }

    // ── Upload ───────────────────────────────────────────────────────────────

    public async Task<CaseDocumentUploadResult> UploadAsync(
        Guid caseId,
        string docType,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant context required.");

        // Validate case ownership
        var existingCase = await _db.InsolvencyCases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId && c.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException($"Case {caseId} not found.");

        // Validate extension
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext.Length == 0) ext = ".bin";
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not supported. " +
                $"Allowed: {string.Join(", ", AllowedExtensions)}");

        // Sanitise docType — fall back to "Other" if empty
        if (string.IsNullOrWhiteSpace(docType)) docType = "Other";

        // 1. Ensure folder structure
        await EnsureFolderAsync(caseId, docType, ct);

        // 2. Upload to canonical storage path
        var docId = Guid.NewGuid();
        var storageKey = CaseStorageKeys.Document(caseId, docType, docId, ext);

        // Buffer the stream so we can both upload and extract text
        byte[] fileBytes;
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }
        contentType = ResolveMimeType(contentType, ext);
        await _storage.UploadAsync(storageKey, new MemoryStream(fileBytes), contentType, ct);

        // 3. Save InsolvencyDocument record
        var email     = _currentUser.Email ?? "System";
        var document  = new InsolvencyDocument
        {
            Id             = docId,
            TenantId       = tenantId,
            CaseId         = caseId,
            SourceFileName = fileName,
            StorageKey     = storageKey,
            DocType        = docType,
            Purpose        = "Uploaded",
            UploadedBy     = email,
            UploadedAt     = DateTime.UtcNow,
            CreatedOn      = DateTime.UtcNow,
            CreatedBy      = email,
        };
        _db.InsolvencyDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        // 4. Extract text (PDFs only)
        var extractedText = string.Empty;
        if (ext == ".pdf" && fileBytes.Length > 0)
        {
            try
            {
                extractedText = ExtractPdfText(fileBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF text extraction failed for {FileName}", fileName);
            }
        }

        // 5. Build annotation context from IncomingDocumentProfile (if any)
        string? annotationContext = null;
        bool annotationsApplied   = false;
        try
        {
            var profile = await _profiles.GetProfileAsync(docType, ct);
            if (profile?.AnnotationsJson is not null)
            {
                annotationContext  = BuildAnnotationContext(profile.AnnotationsJson);
                annotationsApplied = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load annotation profile for docType '{DocType}'", docType);
        }

        // 6. AI analysis
        AiDocumentAnalysisService.AiAnalysisResult? aiResult = null;
        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            try
            {
                aiResult = await _aiAnalysis.AnalyzeAsync(extractedText, fileName, annotationContext, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI analysis failed for uploaded document '{FileName}'", fileName);
            }
        }

        // 7. Persist AI results
        if (aiResult is not null)
        {
            var parties = aiResult.Parties.Count > 0
                ? JsonSerializer.Serialize(aiResult.Parties)
                : null;

            document.RawExtraction          = extractedText.Length > 10_000
                ? extractedText[..10_000]
                : extractedText;
            document.Summary                = BuildSummary(aiResult, existingCase);
            document.ClassificationConfidence = (int)(aiResult.Confidence * 100);
            document.PartiesExtractedJson   = parties;
            document.FieldsExtractedJson    = JsonSerializer.Serialize(new
            {
                aiResult.CaseNumber,
                aiResult.DebtorName,
                aiResult.DebtorCui,
                aiResult.CourtName,
                aiResult.CourtSection,
                aiResult.JudgeSyndic,
                aiResult.ProcedureType,
                aiResult.OpeningDate,
                aiResult.NextHearingDate,
                aiResult.ClaimsDeadline,
                aiResult.ContestationsDeadline,
                aiResult.Confidence,
            });

            await _db.SaveChangesAsync(ct);
        }

        // 8. Audit
        await _audit.LogAsync(new AuditEntry
        {
            Action      = "Document.Uploaded",
            Description = $"Document '{fileName}' (type: {docType}) uploaded to case '{existingCase.CaseNumber}'. " +
                $"AI extraction: {(aiResult is not null ? $"done ({aiResult.Confidence:P0} confidence)" : "skipped")}.",
            EntityType  = "InsolvencyDocument",
            EntityId    = docId,
            EntityName  = fileName,
            CaseNumber  = existingCase.CaseNumber,
            NewValues   = new { docId, docType, storageKey, aiExtracted = aiResult is not null },
            Severity    = "Info",
            Category    = "Document",
        });

        _logger.LogInformation(
            "Document '{FileName}' ({DocType}) uploaded to case {CaseId} → {StorageKey}",
            fileName, docType, caseId, storageKey);

        return new CaseDocumentUploadResult
        {
            DocumentId         = docId,
            FileName           = fileName,
            StorageKey         = storageKey,
            DocType            = docType,
            FileSizeBytes      = fileBytes.Length,
            AiExtracted        = aiResult is not null,
            AiSummary          = document.Summary,
            AnnotationsApplied = annotationsApplied,
            AiConfidence       = aiResult?.Confidence,
            FieldsExtractedJson = document.FieldsExtractedJson,
        };
    }

    // ── Folder structure ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task EnsureCaseFolderStructureAsync(Guid caseId, CancellationToken ct = default)
    {
        foreach (var folder in CaseStorageKeys.StandardFolders(caseId))
        {
            try { await _storage.EnsureFolderAsync(folder, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ensure folder {Folder}", folder);
            }
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private async Task EnsureFolderAsync(Guid caseId, string docType, CancellationToken ct)
    {
        try
        {
            await _storage.EnsureFolderAsync(CaseStorageKeys.Folder(caseId, docType), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure folder for case {CaseId}/{DocType}", caseId, docType);
        }
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf    = PdfDocument.Open(stream);
        var sb           = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.Append(string.Join(" ", page.GetWords().Select(w => w.Text)));
            sb.Append('\n');
        }
        return sb.ToString().Trim();
    }

    private static string BuildAnnotationContext(string annotationsJson)
    {
        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(annotationsJson);
            var arrEl      = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? doc.RootElement
                : doc.RootElement.TryGetProperty("annotations", out var inner)
                    ? inner
                    : default;

            if (arrEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                return string.Empty;

            var lines = new List<string>();
            foreach (var item in arrEl.EnumerateArray())
            {
                var field = item.TryGetProperty("field", out var f) ? f.GetString() : null;
                var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
                var y     = item.TryGetProperty("y", out var yp) ? yp.GetDouble() : 0;
                if (field is not null)
                    lines.Add($"- {label ?? field}: approximately {y:P0} from top of page");
            }
            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }
        catch { return string.Empty; }
    }

    private static string BuildSummary(
        AiDocumentAnalysisService.AiAnalysisResult ai, InsolvencyCase existingCase)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ai.DocType))    parts.Add($"Type: {ai.DocType}");
        if (!string.IsNullOrWhiteSpace(ai.CaseNumber)) parts.Add($"Case: {ai.CaseNumber}");
        if (!string.IsNullOrWhiteSpace(ai.DebtorName)) parts.Add($"Debtor: {ai.DebtorName}");
        if (ai.OpeningDate.HasValue)                   parts.Add($"Opened: {ai.OpeningDate:dd.MM.yyyy}");
        if (ai.ClaimsDeadline.HasValue)                parts.Add($"Claims deadline: {ai.ClaimsDeadline:dd.MM.yyyy}");
        return parts.Count > 0 ? string.Join("; ", parts) : $"Document filed to case {existingCase.CaseNumber}";
    }

    private static string ResolveMimeType(string contentType, string ext) =>
        string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream"
            ? ext switch
            {
                ".pdf"  => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc"  => "application/msword",
                ".png"  => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" => "image/tiff",
                _       => "application/octet-stream",
            }
            : contentType;
}
