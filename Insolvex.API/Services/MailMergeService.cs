using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.Configuration;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.API.Services;

/// <summary>
/// Generates insolvency documents by mail-merging templates from Templates-Ro.
/// Uses TemplateGenerationService for actual DOCX placeholder replacement.
/// Per InsolvencyAppRules section 7: fail-fast if required fields missing,
/// creates blocking tasks, tracks GeneratedLetter records.
/// </summary>
public class MailMergeService
{
  private readonly ApplicationDbContext _db;
  private readonly IFileStorageService _storage;
  private readonly TemplateGenerationService _templateGen;
  private readonly HtmlPdfService _htmlPdf;
  private readonly MailMergeOptions _options;
  private readonly IWebHostEnvironment _env;
  private readonly ILogger<MailMergeService> _logger;

  /// <summary>
  /// Maps each <see cref="DocumentTemplateType"/> to its template filename in Templates-Ro.
  /// </summary>
  public static readonly Dictionary<DocumentTemplateType, string> TemplateFileNames = new()
  {
    [DocumentTemplateType.CourtOpeningDecision] = "0.sentinta Aderom Mio.pdf",
    [DocumentTemplateType.CreditorNotificationBpi] = "1.Notificare creditori deschidere procedura_BPI.doc",
    [DocumentTemplateType.ReportArt97] = "2.Raport 40 zile_AM.doc",
    [DocumentTemplateType.PreliminaryClaimsTable] = "3.Tabel prel.doc",
    [DocumentTemplateType.CreditorsMeetingMinutes] = "4.proces verbal AGC confirmare lichidator.doc",
    [DocumentTemplateType.DefinitiveClaimsTable] = "5.Tabel DEFINITIV.doc",
    [DocumentTemplateType.FinalReportArt167] = "7.Raport final_AM.doc",
    // HTML→PDF templates (rendered via HtmlPdfService)
    [DocumentTemplateType.CreditorNotificationHtml] = "notificare_deschidere_procedura_template.html",
  };

  /// <summary>
  /// Maps detected doc type strings (from classification) to template types.
  /// </summary>
  public static readonly Dictionary<string, DocumentTemplateType> DocTypeToTemplate = new(StringComparer.OrdinalIgnoreCase)
  {
    ["court_decision"] = DocumentTemplateType.CourtOpeningDecision,
    ["court_opening_decision"] = DocumentTemplateType.CourtOpeningDecision,
    ["notification"] = DocumentTemplateType.CreditorNotificationBpi,
    ["notification_opening"] = DocumentTemplateType.CreditorNotificationBpi,
    ["report"] = DocumentTemplateType.ReportArt97,
    ["report_art_97"] = DocumentTemplateType.ReportArt97,
    ["claims_table"] = DocumentTemplateType.PreliminaryClaimsTable,
    ["claims_table_preliminary"] = DocumentTemplateType.PreliminaryClaimsTable,
    ["claims_table_definitive"] = DocumentTemplateType.DefinitiveClaimsTable,
    ["creditors_meeting_minutes"] = DocumentTemplateType.CreditorsMeetingMinutes,
    ["final_report_art_167"] = DocumentTemplateType.FinalReportArt167,
  };

  /// <summary>Template types that are critical per stage � deadlines must never be missed.</summary>
  public static readonly HashSet<DocumentTemplateType> CriticalTemplateTypes = new()
  {
    DocumentTemplateType.CreditorNotificationBpi,
    DocumentTemplateType.CreditorNotificationHtml,
    DocumentTemplateType.PreliminaryClaimsTable,
    DocumentTemplateType.DefinitiveClaimsTable,
    DocumentTemplateType.FinalReportArt167,
  };

  /// <summary>Template types that use the HTML→PDF rendering pipeline instead of DOCX.</summary>
  public static readonly HashSet<DocumentTemplateType> HtmlTemplateTypes = new()
  {
    DocumentTemplateType.CreditorNotificationHtml,
  };

  public MailMergeService(
      ApplicationDbContext db,
      IFileStorageService storage,
      TemplateGenerationService templateGen,
      HtmlPdfService htmlPdf,
      IOptions<MailMergeOptions> options,
      IWebHostEnvironment env,
      ILogger<MailMergeService> logger)
  {
    _db = db;
    _storage = storage;
    _templateGen = templateGen;
    _htmlPdf = htmlPdf;
    _options = options.Value;
    _env = env;
    _logger = logger;
  }

  /// <summary>Try to resolve a DocumentTemplateType from a detected doc type string.</summary>
  public static DocumentTemplateType? ResolveTemplateType(string? docType)
  {
    if (string.IsNullOrWhiteSpace(docType)) return null;
    return DocTypeToTemplate.TryGetValue(docType, out var tt) ? tt : null;
  }

  /// <summary>Get the full path to a template file on disk.</summary>
  public string GetTemplatePath(DocumentTemplateType templateType)
  {
    if (!TemplateFileNames.TryGetValue(templateType, out var fileName))
      throw new ArgumentException($"No template file mapped for {templateType}");

    return Path.Combine(_env.ContentRootPath, _options.TemplatesPath, fileName);
  }

  /// <summary>
  /// Resolve the effective template path for generation, checking DB overrides
  /// (tenant-specific first, then global) before falling back to disk.
  /// Returns a tuple: (filePath, isTempFile) — caller must delete temp files.
  /// </summary>
  public async Task<(string Path, bool IsTemp)> GetEffectiveTemplatePathAsync(
      DocumentTemplateType templateType, Guid tenantId, CancellationToken ct = default)
  {
    // 1. Tenant-specific DB override
    var dbTemplate = await _db.DocumentTemplates
        .IgnoreQueryFilters()
        .Where(t => t.TenantId == tenantId && t.TemplateType == templateType && t.IsActive)
        .FirstOrDefaultAsync(ct);

    // 2. Global DB override
    if (dbTemplate == null)
    {
      dbTemplate = await _db.DocumentTemplates
          .IgnoreQueryFilters()
          .Where(t => t.TenantId == null && t.TemplateType == templateType && t.IsActive)
          .FirstOrDefaultAsync(ct);
    }

    // 3. Use DB template — download to temp file
    if (dbTemplate != null && !string.IsNullOrEmpty(dbTemplate.StorageKey)
        && await _storage.ExistsAsync(dbTemplate.StorageKey))
    {
      var ext = Path.GetExtension(dbTemplate.FileName);
      var tempPath = Path.Combine(Path.GetTempPath(), $"tpl_{templateType}_{Guid.NewGuid()}{ext}");
      await using var src = await _storage.DownloadAsync(dbTemplate.StorageKey);
      await using var dest = File.Create(tempPath);
      await src.CopyToAsync(dest, ct);
      _logger.LogInformation("Using DB template override ({Source}) for {Type}: {File}",
          dbTemplate.TenantId.HasValue ? "tenant" : "global", templateType, dbTemplate.FileName);
      return (tempPath, true);
    }

    // 4. Fall back to disk
    return (GetTemplatePath(templateType), false);
  }

  /// <summary>
  /// Get all available templates, merging disk files with DB overrides for a given tenant.
  /// </summary>
  public async Task<List<TemplateInfoFull>> GetAvailableTemplatesAsync(Guid? tenantId, CancellationToken ct = default)
  {
    var dbTemplates = await _db.DocumentTemplates
        .IgnoreQueryFilters()
        .Where(t => t.TenantId == null || t.TenantId == tenantId)
        .ToListAsync(ct);

    var result = new List<TemplateInfoFull>();
    foreach (var (type, fileName) in TemplateFileNames)
    {
      var diskPath = Path.Combine(_env.ContentRootPath, _options.TemplatesPath, fileName);
      var diskExists = File.Exists(diskPath);
      var diskSize = diskExists ? new FileInfo(diskPath).Length : 0L;

      var tenantOverride = dbTemplates.FirstOrDefault(t => t.TenantId == tenantId && t.TemplateType == type);
      var globalOverride = dbTemplates.FirstOrDefault(t => t.TenantId == null && t.TemplateType == type);

      result.Add(new TemplateInfoFull
      {
        TemplateType = type,
        DefaultFileName = fileName,
        DiskExists = diskExists,
        DiskFileSizeBytes = diskSize,
        TenantOverrideId = tenantOverride?.Id,
        TenantOverrideFileName = tenantOverride?.FileName,
        TenantOverrideFileSizeBytes = tenantOverride?.FileSizeBytes ?? 0,
        TenantOverrideVersion = tenantOverride?.Version ?? 0,
        GlobalOverrideId = globalOverride?.Id,
        GlobalOverrideFileName = globalOverride?.FileName,
        GlobalOverrideFileSizeBytes = globalOverride?.FileSizeBytes ?? 0,
        GlobalOverrideVersion = globalOverride?.Version ?? 0,
        EffectiveSource = tenantOverride != null ? "tenant" : globalOverride != null ? "global-db" : diskExists ? "disk" : "missing",
      });
    }
    return result;
  }

  /// <summary>
  /// Generate a document from a template for a case with actual mail-merge.
  /// Creates a GeneratedLetter record for tracking.
  /// Validates required merge fields; creates blocking task on failure.
  /// </summary>
  public async Task<GeneratedDocument> GenerateAsync(
      DocumentTemplateType templateType,
    InsolvencyCase insolvencyCase,
      Company? debtorCompany,
      InsolvencyFirm? firm,
      CancellationToken ct = default)
  {
    var (templatePath, isTempFile) = await GetEffectiveTemplatePathAsync(templateType, insolvencyCase.TenantId, ct);
    if (!File.Exists(templatePath))
    {
      _logger.LogWarning("Template file not found: {Path}", templatePath);
      throw new FileNotFoundException($"Template not found: {templatePath}");
    }

    // Build merge data from case
    var mergeData = await _templateGen.BuildMergeDataAsync(insolvencyCase.Id);

    // Check if DB template has required merge fields schema
    var dbTemplate = await _db.DocumentTemplates.AsNoTracking()
        .FirstOrDefaultAsync(t => t.TemplateType == templateType, ct);

    if (dbTemplate != null)
    {
      var missingFields = _templateGen.ValidateMergeFields(dbTemplate.MergeFieldsJson, mergeData);
      if (missingFields.Count > 0)
      {
        _logger.LogWarning("Missing merge fields for {Type}: {Fields}", templateType, string.Join(", ", missingFields));

        // Create blocking "Fix merge fields" task per InsolvencyAppRules section 7
        await CreateBlockingMergeFieldTaskAsync(insolvencyCase, templateType, missingFields);

        // Record failed GeneratedLetter
        var failedLetter = CreateGeneratedLetter(insolvencyCase, templateType, dbTemplate?.Id);
        failedLetter.DeliveryStatus = "Failed";
        failedLetter.ErrorMessage = $"Missing required merge fields: {string.Join(", ", missingFields)}";
        _db.GeneratedLetters.Add(failedLetter);
        await _db.SaveChangesAsync(ct);

        throw new InvalidOperationException(
            $"Cannot generate {templateType}: missing fields [{string.Join(", ", missingFields)}]. Blocking task created.");
      }
    }

    // Render with actual placeholder replacement
    var outputFileName = GenerateOutputFileName(templateType, insolvencyCase);
    var storageKey = $"{insolvencyCase.Id}/{outputFileName}";

    RenderedDocumentResult renderResult;
    if (HtmlTemplateTypes.Contains(templateType))
    {
      // HTML → PDF pipeline via PuppeteerSharp
      renderResult = await _htmlPdf.RenderHtmlToPdfAsync(templatePath, mergeData, storageKey, ct);
    }
    else
    {
      // DOCX / legacy DOC / PDF pipeline
      renderResult = await _templateGen.RenderDocxAsync(templatePath, mergeData, storageKey);
    }

    if (isTempFile) { try { File.Delete(templatePath); } catch { /* ignore */ } }

    if (!renderResult.Success)
    {
      _logger.LogError("Template render failed for {Type}: {Error}", templateType, renderResult.Error);

      var failedLetter = CreateGeneratedLetter(insolvencyCase, templateType, dbTemplate?.Id);
      failedLetter.DeliveryStatus = "Failed";
      failedLetter.ErrorMessage = renderResult.Error;
      _db.GeneratedLetters.Add(failedLetter);
      await _db.SaveChangesAsync(ct);

      throw new InvalidOperationException($"Template render failed: {renderResult.Error}");
    }

    // Create successful GeneratedLetter record
    var letter = CreateGeneratedLetter(insolvencyCase, templateType, dbTemplate?.Id);
    letter.StorageKey = renderResult.StorageKey;
    letter.FileName = outputFileName;
    letter.FileSizeBytes = renderResult.FileSizeBytes;
    letter.ContentType = renderResult.ContentType;
    letter.MergeDataJson = renderResult.MergeDataJson ?? System.Text.Json.JsonSerializer.Serialize(mergeData);
    letter.DeliveryStatus = "Rendered";
    letter.IsCritical = CriticalTemplateTypes.Contains(templateType);
    _db.GeneratedLetters.Add(letter);
    await _db.SaveChangesAsync(ct);

    _logger.LogInformation("Generated {Type} for case {CaseId}: {FileName} ({Size} bytes)",
           templateType, insolvencyCase.Id, outputFileName, renderResult.FileSizeBytes);

    return new GeneratedDocument
    {
      TemplateType = templateType,
      StorageKey = renderResult.StorageKey,
      FileName = outputFileName,
      FileSizeBytes = renderResult.FileSizeBytes,
    };
  }

  /// <summary>
  /// Generate all applicable key documents for a newly created case.
  /// </summary>
  public async Task<List<GeneratedDocument>> GenerateKeyDocumentsForCaseAsync(
      InsolvencyCase insolvencyCase,
      Company? debtorCompany,
      InsolvencyFirm? firm,
      string? detectedDocType,
      CancellationToken ct = default)
  {
    var results = new List<GeneratedDocument>();

    var typesToGenerate = new List<DocumentTemplateType>
     {
       DocumentTemplateType.CreditorNotificationBpi,
        };

    var resolved = ResolveTemplateType(detectedDocType);
    if (resolved == DocumentTemplateType.CourtOpeningDecision)
    {
      typesToGenerate.Add(DocumentTemplateType.ReportArt97);
    }

    foreach (var tt in typesToGenerate)
    {
      try
      {
        var doc = await GenerateAsync(tt, insolvencyCase, debtorCompany, firm, ct);
        results.Add(doc);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate template {Type} for case {CaseId}", tt, insolvencyCase.Id);
      }
    }

    return results;
  }

  /// <summary>Get all available templates and their paths.</summary>
  public List<TemplateInfo> GetAvailableTemplates()
  {
    var result = new List<TemplateInfo>();
    foreach (var (type, fileName) in TemplateFileNames)
    {
      var path = Path.Combine(_env.ContentRootPath, _options.TemplatesPath, fileName);
      result.Add(new TemplateInfo
      {
        TemplateType = type,
        FileName = fileName,
        Exists = File.Exists(path),
        FileSizeBytes = File.Exists(path) ? new FileInfo(path).Length : 0,
      });
    }
    return result;
  }

  /// <summary>
  /// Overload that loads the case from DB by ID, then delegates to GenerateAsync.
  /// Allows controllers to stay thin without injecting ApplicationDbContext.
  /// </summary>
  public async Task<GeneratedDocument> GenerateForCaseAsync(
      Guid caseId, DocumentTemplateType templateType, CancellationToken ct = default)
  {
    var cas = await _db.InsolvencyCases.Include(c => c.Company).FirstOrDefaultAsync(c => c.Id == caseId, ct)
        ?? throw new KeyNotFoundException($"Case {caseId} not found");
    var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync(ct);
    return await GenerateAsync(templateType, cas, cas.Company, firm, ct);
  }

  /// <summary>
  /// Overload that loads the case from DB by ID, then delegates to GenerateKeyDocumentsForCaseAsync.
  /// </summary>
  public async Task<List<GeneratedDocument>> GenerateKeyDocumentsForCaseIdAsync(
      Guid caseId, string? detectedDocType, CancellationToken ct = default)
  {
    var cas = await _db.InsolvencyCases.Include(c => c.Company).FirstOrDefaultAsync(c => c.Id == caseId, ct)
        ?? throw new KeyNotFoundException($"Case {caseId} not found");
    var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync(ct);
    return await GenerateKeyDocumentsForCaseAsync(cas, cas.Company, firm, detectedDocType, ct);
  }

  // ?? Private helpers ?????????????????????????????????????

  private GeneratedLetter CreateGeneratedLetter(InsolvencyCase cas, DocumentTemplateType templateType, Guid? dbTemplateId)
  {
    return new GeneratedLetter
    {
      TenantId = cas.TenantId,
      CaseId = cas.Id,
      TemplateId = dbTemplateId,
      TemplateType = templateType,
      Stage = cas.Stage,
      RenderedAt = DateTime.UtcNow,
      IsCritical = CriticalTemplateTypes.Contains(templateType),
    };
  }

  private async System.Threading.Tasks.Task CreateBlockingMergeFieldTaskAsync(
      InsolvencyCase cas, DocumentTemplateType templateType, List<string> missingFields)
  {
    if (cas.CompanyId == null) return;

    var alreadyExists = await _db.CompanyTasks
       .AnyAsync(t => t.CaseId == cas.Id
  && t.Title.Contains("Fix merge fields")
            && t.Title.Contains(templateType.ToString())
              && t.Status != TaskStatus.Done);

    if (alreadyExists) return;

    _db.CompanyTasks.Add(new CompanyTask
    {
      TenantId = cas.TenantId,
      CompanyId = cas.CompanyId.Value,
      CaseId = cas.Id,
      Title = $"Fix merge fields: {templateType} � {cas.CaseNumber}",
      Description = $"Cannot generate {templateType}: missing required fields: {string.Join(", ", missingFields)}. " +
$"Please complete the case data and retry template generation.",
      Category = "Document",
      Stage = cas.Stage,
      Deadline = DateTime.UtcNow.AddDays(1),
      DeadlineSource = "CompanyDefault",
      IsCriticalDeadline = CriticalTemplateTypes.Contains(templateType),
      Status = TaskStatus.Blocked,
      AssignedToUserId = cas.AssignedToUserId,
    });

    await _db.SaveChangesAsync();
  }

  private static string GenerateOutputFileName(DocumentTemplateType templateType, InsolvencyCase cas)
  {
    var sanitizedDebtor = (cas.DebtorName ?? "debtor")
        .Replace(" ", "_").Replace("/", "-");

    return templateType switch
    {
      DocumentTemplateType.CourtOpeningDecision =>
     $"Sentinta_deschidere_{sanitizedDebtor}.pdf",
      DocumentTemplateType.CreditorNotificationBpi =>
$"Notificare_creditori_BPI_{sanitizedDebtor}.doc",
      DocumentTemplateType.CreditorNotificationHtml =>
          $"Notificare_deschidere_procedura_{sanitizedDebtor}.pdf",
      DocumentTemplateType.ReportArt97 =>
               $"Raport_Art97_{sanitizedDebtor}.doc",
      DocumentTemplateType.PreliminaryClaimsTable =>
          $"Tabel_preliminar_{sanitizedDebtor}.doc",
      DocumentTemplateType.CreditorsMeetingMinutes =>
          $"PV_AGC_{sanitizedDebtor}.doc",
      DocumentTemplateType.DefinitiveClaimsTable =>
    $"Tabel_definitiv_{sanitizedDebtor}.doc",
      DocumentTemplateType.FinalReportArt167 =>
       $"Raport_final_Art167_{sanitizedDebtor}.doc",
      _ => $"Document_{sanitizedDebtor}.doc",
    };
  }
}

// ?? Result models ???????????????????????????????????????

public class GeneratedDocument
{
  public DocumentTemplateType TemplateType { get; set; }
  public string StorageKey { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public long FileSizeBytes { get; set; }
}

public class TemplateInfo
{
  public DocumentTemplateType TemplateType { get; set; }
  public string FileName { get; set; } = string.Empty;
  public bool Exists { get; set; }
  public long FileSizeBytes { get; set; }
}

/// <summary>Full template metadata merging disk + DB override info.</summary>
public class TemplateInfoFull
{
  public DocumentTemplateType TemplateType { get; set; }
  public string DefaultFileName { get; set; } = string.Empty;
  public bool DiskExists { get; set; }
  public long DiskFileSizeBytes { get; set; }
  public Guid? TenantOverrideId { get; set; }
  public string? TenantOverrideFileName { get; set; }
  public long TenantOverrideFileSizeBytes { get; set; }
  public int TenantOverrideVersion { get; set; }
  public Guid? GlobalOverrideId { get; set; }
  public string? GlobalOverrideFileName { get; set; }
  public long GlobalOverrideFileSizeBytes { get; set; }
  public int GlobalOverrideVersion { get; set; }
  /// <summary>"tenant" | "global-db" | "disk" | "missing"</summary>
  public string EffectiveSource { get; set; } = "missing";
}
