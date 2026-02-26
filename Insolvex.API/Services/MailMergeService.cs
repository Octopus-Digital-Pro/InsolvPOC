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

    /// <summary>Template types that are critical per stage — deadlines must never be missed.</summary>
    public static readonly HashSet<DocumentTemplateType> CriticalTemplateTypes = new()
    {
 DocumentTemplateType.CreditorNotificationBpi,
        DocumentTemplateType.PreliminaryClaimsTable,
   DocumentTemplateType.DefinitiveClaimsTable,
        DocumentTemplateType.FinalReportArt167,
    };

    public MailMergeService(
        ApplicationDbContext db,
        IFileStorageService storage,
     TemplateGenerationService templateGen,
      IOptions<MailMergeOptions> options,
        IWebHostEnvironment env,
 ILogger<MailMergeService> logger)
    {
        _db = db;
        _storage = storage;
        _templateGen = templateGen;
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
        var templatePath = GetTemplatePath(templateType);
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

        var renderResult = await _templateGen.RenderDocxAsync(templatePath, mergeData, storageKey);

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
            Title = $"Fix merge fields: {templateType} — {cas.CaseNumber}",
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
