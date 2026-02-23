using Insolvex.Core.Abstractions;
using Insolvex.Core.Configuration;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insolvex.API.Services;

/// <summary>
/// Generates insolvency documents by copying templates from the configured
/// templates folder and saving them to file storage (local disk or S3).
/// Mail-merge placeholder replacement is deferred to a future iteration
/// when a proper document-processing library is integrated.
/// Currently: copies templates as-is into the output location per case.
/// </summary>
public class MailMergeService
{
    private readonly IFileStorageService _storage;
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

    public MailMergeService(
    IFileStorageService storage,
        IOptions<MailMergeOptions> options,
      IWebHostEnvironment env,
        ILogger<MailMergeService> logger)
    {
        _storage = storage;
        _options = options.Value;
        _env = env;
        _logger = logger;
  }

    /// <summary>
    /// Try to resolve a <see cref="DocumentTemplateType"/> from a detected doc type string.
    /// </summary>
    public static DocumentTemplateType? ResolveTemplateType(string? docType)
    {
        if (string.IsNullOrWhiteSpace(docType)) return null;
        return DocTypeToTemplate.TryGetValue(docType, out var tt) ? tt : null;
    }

    /// <summary>
    /// Get the full path to a template file on disk.
    /// </summary>
    public string GetTemplatePath(DocumentTemplateType templateType)
{
        if (!TemplateFileNames.TryGetValue(templateType, out var fileName))
     throw new ArgumentException($"No template file mapped for {templateType}");

   return Path.Combine(_env.ContentRootPath, _options.TemplatesPath, fileName);
    }

    /// <summary>
    /// Generate a document from a template for a case.
    /// Currently copies the template as-is; placeholder replacement deferred to future iteration.
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

// For now: copy template as-is to storage (mail-merge placeholder replacement deferred)
     return await CopyTemplateToStorage(templatePath, templateType, insolvencyCase, ct);
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

    // Always generate creditor notification for new cases
        var typesToGenerate = new List<DocumentTemplateType>
    {
            DocumentTemplateType.CreditorNotificationBpi,
        };

     // If the uploaded document is a court opening decision, also generate the report template
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

    /// <summary>
    /// Get all available templates and their paths.
    /// </summary>
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

    // ?? Private helpers ??????????????????????????????????

    private async Task<GeneratedDocument> CopyTemplateToStorage(
        string templatePath,
        DocumentTemplateType templateType,
        InsolvencyCase cas,
        CancellationToken ct)
    {
        _logger.LogInformation("Copying template {Type} for case {CaseNumber}", templateType, cas.CaseNumber);

  var ext = Path.GetExtension(templatePath);
        var outputFileName = GenerateOutputFileName(templateType, cas);
      var storageKey = $"{cas.Id}/{outputFileName}";
var contentType = ext switch
        {
  ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      _ => "application/octet-stream",
        };

        await using var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
     await _storage.UploadAsync(storageKey, fs, contentType, ct);

        return new GeneratedDocument
        {
            TemplateType = templateType,
    StorageKey = storageKey,
      FileName = outputFileName,
            FileSizeBytes = new FileInfo(templatePath).Length,
      };
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

// ?? Result models ????????????????????????????????????

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
