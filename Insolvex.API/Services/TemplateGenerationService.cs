using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Services;

/// <summary>
/// Handles DOCX mail-merge placeholder replacement using DocumentFormat.OpenXml.
/// Templates use {{FieldName}} placeholders that get replaced with case/party/firm data.
/// 
/// Per InsolvencyAppRules section 7:
///  - Templates have required merge fields (schema)
///  - Generation fails fast if required fields are missing ? creates a blocking task
///  - Critical templates cannot miss deadlines
///  
/// TODO: For production, integrate a more robust document generation library
///    (e.g., Aspose.Words, DocX, or a headless LibreOffice for PDF conversion).
/// </summary>
public class TemplateGenerationService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<TemplateGenerationService> _logger;

    public TemplateGenerationService(
     ApplicationDbContext db,
        IFileStorageService storage,
 ILogger<TemplateGenerationService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Build the full merge data dictionary for a case.
    /// </summary>
  public async Task<Dictionary<string, string>> BuildMergeDataAsync(
     Guid caseId, Guid? recipientPartyId = null)
    {
        var c = await _db.InsolvencyCases
          .Include(x => x.Company)
            .Include(x => x.Parties).ThenInclude(p => p.Company)
            .FirstOrDefaultAsync(x => x.Id == caseId);

        if (c == null) return new();

        var firm = await _db.InsolvencyFirms.FirstOrDefaultAsync();

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Case fields
            ["CaseNumber"] = c.CaseNumber,
     ["DebtorName"] = c.DebtorName,
            ["DebtorCui"] = c.DebtorCui ?? "",
            ["CourtName"] = c.CourtName ?? "",
      ["CourtSection"] = c.CourtSection ?? "",
            ["JudgeSyndic"] = c.JudgeSyndic ?? "",
       ["ProcedureType"] = c.ProcedureType.ToString(),
         ["LawReference"] = c.LawReference ?? "Legea 85/2014",
   ["NoticeDate"] = c.NoticeDate?.ToString("dd.MM.yyyy") ?? "",
         ["OpeningDate"] = c.OpeningDate?.ToString("dd.MM.yyyy") ?? "",
            ["NextHearingDate"] = c.NextHearingDate?.ToString("dd.MM.yyyy") ?? "",
            ["ClaimsDeadline"] = c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "",
            ["ContestationsDeadline"] = c.ContestationsDeadline?.ToString("dd.MM.yyyy") ?? "",
            ["BpiPublicationNo"] = c.BpiPublicationNo ?? "",
      ["BpiPublicationDate"] = c.BpiPublicationDate?.ToString("dd.MM.yyyy") ?? "",
   ["OpeningDecisionNo"] = c.OpeningDecisionNo ?? "",

        // Practitioner fields
       ["PractitionerName"] = c.PractitionerName ?? firm?.FirmName ?? "",
 ["PractitionerRole"] = c.PractitionerRole ?? "",
     ["PractitionerFiscalId"] = c.PractitionerFiscalId ?? firm?.CuiRo ?? "",
      ["PractitionerDecisionNo"] = c.PractitionerDecisionNo ?? "",

 // Financial fields
  ["TotalClaimsRon"] = c.TotalClaimsRon?.ToString("N2") ?? "",
   ["SecuredClaimsRon"] = c.SecuredClaimsRon?.ToString("N2") ?? "",
        ["UnsecuredClaimsRon"] = c.UnsecuredClaimsRon?.ToString("N2") ?? "",

            // Debtor company fields
  ["DebtorAddress"] = c.Company?.Address ?? "",
   ["DebtorLocality"] = c.Company?.Locality ?? "",
     ["DebtorCounty"] = c.Company?.County ?? "",
    ["DebtorTradeRegisterNo"] = c.Company?.TradeRegisterNo ?? "",
        ["DebtorCaen"] = c.Company?.Caen ?? "",

      // Firm fields
            ["FirmName"] = firm?.FirmName ?? "",
 ["FirmCui"] = firm?.CuiRo ?? "",
            ["FirmAddress"] = firm?.Address ?? "",
     ["FirmLocality"] = firm?.Locality ?? "",
            ["FirmCounty"] = firm?.County ?? "",
    ["FirmPhone"] = firm?.Phone ?? "",
            ["FirmEmail"] = firm?.Email ?? "",
      ["FirmIban"] = firm?.Iban ?? "",
            ["FirmBankName"] = firm?.BankName ?? "",

            // Date fields
  ["CurrentDate"] = DateTime.UtcNow.ToString("dd.MM.yyyy"),
            ["CurrentYear"] = DateTime.UtcNow.Year.ToString(),
        };

        // Add recipient party data if specified
  if (recipientPartyId.HasValue)
        {
    var party = c.Parties.FirstOrDefault(p => p.Id == recipientPartyId.Value);
      if (party != null)
            {
           data["RecipientName"] = party.Name ?? party.Company?.Name ?? "";
    data["RecipientAddress"] = party.Address ?? party.Company?.Address ?? "";
       data["RecipientEmail"] = party.Email ?? party.Company?.Email ?? "";
   data["RecipientIdentifier"] = party.Identifier ?? party.Company?.CuiRo ?? "";
          data["RecipientRole"] = party.Role.ToString();
            }
        }

        // Add all creditor parties as a list
        var creditors = c.Parties
      .Where(p => p.Role is CasePartyRole.SecuredCreditor or CasePartyRole.UnsecuredCreditor
    or CasePartyRole.BudgetaryCreditor or CasePartyRole.EmployeeCreditor)
            .ToList();

        for (var i = 0; i < creditors.Count; i++)
        {
  var cr = creditors[i];
     data[$"Creditor{i + 1}_Name"] = cr.Name ?? cr.Company?.Name ?? "";
       data[$"Creditor{i + 1}_Amount"] = cr.ClaimAmountRon?.ToString("N2") ?? "";
    data[$"Creditor{i + 1}_Priority"] = cr.ClaimPriority ?? cr.Role.ToString();
        }
      data["CreditorCount"] = creditors.Count.ToString();

        return data;
    }

    /// <summary>
    /// Render a DOCX template by replacing {{Placeholder}} tokens with merge data.
    /// Returns the path to the rendered output file.
    /// </summary>
    public async Task<RenderedDocumentResult> RenderDocxAsync(
  string templatePath,
   Dictionary<string, string> mergeData,
        string outputStorageKey)
    {
        if (!File.Exists(templatePath))
          return new RenderedDocumentResult { Error = $"Template not found: {templatePath}" };

        var ext = Path.GetExtension(templatePath).ToLowerInvariant();

    // For .doc files (legacy binary format), copy as-is — real merge requires conversion
        // TODO: Integrate LibreOffice headless or Aspose for .doc support
        if (ext == ".doc")
        {
      return await CopyAsIsAsync(templatePath, outputStorageKey, mergeData);
        }

    // For .docx files, perform actual placeholder replacement
    if (ext == ".docx")
        {
     return await RenderDocxWithReplacementAsync(templatePath, mergeData, outputStorageKey);
        }

        // For .pdf and other formats, copy as-is
        return await CopyAsIsAsync(templatePath, outputStorageKey, mergeData);
    }

    /// <summary>
  /// Validate that all required merge fields are present in the data.
    /// Returns list of missing fields.
    /// </summary>
    public List<string> ValidateMergeFields(string? mergeFieldsJson, Dictionary<string, string> data)
    {
        if (string.IsNullOrWhiteSpace(mergeFieldsJson)) return new();

        try
{
            var requiredFields = JsonSerializer.Deserialize<List<string>>(mergeFieldsJson) ?? new();
    return requiredFields
    .Where(f => !data.ContainsKey(f) || string.IsNullOrWhiteSpace(data[f]))
         .ToList();
  }
        catch
        {
            return new();
        }
    }

    // ?? Private helpers ?????????????????????????????????????

    private async Task<RenderedDocumentResult> RenderDocxWithReplacementAsync(
     string templatePath, Dictionary<string, string> mergeData, string outputStorageKey)
    {
        try
        {
            // Copy template to a temp file for modification
  var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        File.Copy(templatePath, tempPath, overwrite: true);

          using (var doc = WordprocessingDocument.Open(tempPath, isEditable: true))
            {
       var body = doc.MainDocumentPart?.Document?.Body;
         if (body != null)
         {
       ReplacePlaceholdersInBody(body, mergeData);
   }

      // Also process headers and footers
         if (doc.MainDocumentPart != null)
    {
         foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
           {
     ReplacePlaceholdersInElement(headerPart.Header, mergeData);
         }
        foreach (var footerPart in doc.MainDocumentPart.FooterParts)
        {
   ReplacePlaceholdersInElement(footerPart.Footer, mergeData);
        }
  }

 doc.Save();
            }

     // Upload rendered file to storage
     await using var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
       var contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
       await _storage.UploadAsync(outputStorageKey, fs, contentType);

 var fileInfo = new FileInfo(tempPath);
         var result = new RenderedDocumentResult
            {
        Success = true,
    StorageKey = outputStorageKey,
             FileSizeBytes = fileInfo.Length,
          ContentType = contentType,
  };

      // Cleanup temp file
            try { File.Delete(tempPath); } catch { /* ignore */ }

       return result;
    }
        catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to render DOCX template {Path}", templatePath);
            return new RenderedDocumentResult { Error = ex.Message };
        }
    }

    private static void ReplacePlaceholdersInBody(Body body, Dictionary<string, string> mergeData)
    {
   // OpenXml may split {{Placeholder}} across multiple Run elements.
        // Strategy: get full paragraph text, find placeholders, then replace in the first run
        // and clear subsequent runs that were part of the placeholder.

     foreach (var paragraph in body.Descendants<Paragraph>())
   {
        var runs = paragraph.Elements<Run>().ToList();
      if (runs.Count == 0) continue;

         // Concatenate all run texts
            var fullText = string.Concat(runs.Select(r => r.InnerText));

            // Check if paragraph contains any placeholders
if (!fullText.Contains("{{")) continue;

      // Replace all {{Key}} patterns
         var replaced = Regex.Replace(fullText, @"\{\{(\w+)\}\}", match =>
  {
                var key = match.Groups[1].Value;
    return mergeData.TryGetValue(key, out var val) ? val : match.Value;
          });

          if (replaced == fullText) continue;

      // Set replaced text in first run, clear others
            for (var i = 0; i < runs.Count; i++)
         {
       var textElement = runs[i].GetFirstChild<Text>();
     if (i == 0)
   {
     if (textElement == null)
{
            textElement = new Text();
              runs[i].AppendChild(textElement);
                  }
    textElement.Text = replaced;
               textElement.Space = new DocumentFormat.OpenXml.EnumValue<DocumentFormat.OpenXml.SpaceProcessingModeValues>(
       DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve);
                }
                else
          {
          if (textElement != null) textElement.Text = string.Empty;
    }
    }
        }
    }

    private static void ReplacePlaceholdersInElement(
        DocumentFormat.OpenXml.OpenXmlElement? element, Dictionary<string, string> mergeData)
    {
  if (element == null) return;

        foreach (var paragraph in element.Descendants<Paragraph>())
        {
var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count == 0) continue;

            var fullText = string.Concat(runs.Select(r => r.InnerText));
     if (!fullText.Contains("{{")) continue;

      var replaced = Regex.Replace(fullText, @"\{\{(\w+)\}\}", match =>
     {
     var key = match.Groups[1].Value;
         return mergeData.TryGetValue(key, out var val) ? val : match.Value;
    });

            if (replaced == fullText) continue;

   for (var i = 0; i < runs.Count; i++)
          {
          var textElement = runs[i].GetFirstChild<Text>();
      if (i == 0)
          {
        if (textElement == null)
    {
 textElement = new Text();
         runs[i].AppendChild(textElement);
}
               textElement.Text = replaced;
       }
           else
    {
       if (textElement != null) textElement.Text = string.Empty;
    }
            }
        }
    }

    private async Task<RenderedDocumentResult> CopyAsIsAsync(
    string templatePath, string outputStorageKey, Dictionary<string, string> mergeData)
    {
    // For non-DOCX formats, copy template as-is to storage
        // TODO: Implement real mail-merge for .doc via LibreOffice headless
        _logger.LogInformation("Copying template as-is (no merge): {Path} ? {Key}", templatePath, outputStorageKey);

   var ext = Path.GetExtension(templatePath).ToLowerInvariant();
var contentType = ext switch
        {
        ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
_ => "application/octet-stream",
     };

        await using var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
  await _storage.UploadAsync(outputStorageKey, fs, contentType);

      return new RenderedDocumentResult
        {
  Success = true,
            StorageKey = outputStorageKey,
            FileSizeBytes = new FileInfo(templatePath).Length,
            ContentType = contentType,
   MergeDataJson = JsonSerializer.Serialize(mergeData),
  };
    }
}

/// <summary>Result of rendering a template document.</summary>
public class RenderedDocumentResult
{
  public bool Success { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? MergeDataJson { get; set; }
    public string? Error { get; set; }
}
