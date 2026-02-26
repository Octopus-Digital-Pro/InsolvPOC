using Insolvex.Core.Abstractions;

namespace Insolvex.Data.Services;

/// <summary>
/// Stub document extraction service returning structured JSON.
/// Designed for plugging in real OCR/LLM later.
/// </summary>
public class StubDocumentExtractionService : IDocumentExtractionService
{
  public Task<ExtractionResult> ExtractAsync(byte[] fileContent, string fileName, string? existingDocType = null)
  {
    // Stub: returns minimal but structurally complete extraction
    var result = new ExtractionResult
    {
      DocType = existingDocType ?? "other",
      Confidence = existingDocType != null ? 85 : 50,
      Summary = $"Document '{fileName}' uploaded. Automated extraction pending real ML/LLM integration.",
      Parties = new List<ExtractedParty>
        {
        new() { Name = "Unknown Debtor", Role = "Debtor" },
  },
      Dates = new List<ExtractedDate>
  {
    new() { Date = DateTime.UtcNow, Meaning = "Upload date", Source = "system" },
            },
      Actions = new List<ExtractedAction>
   {
     new() { Action = "Review uploaded document", Deadline = DateTime.UtcNow.AddDays(3), Assignee = "uploader" },
     },
      Fields = new Dictionary<string, string>
      {
        ["fileName"] = fileName,
        ["fileSize"] = fileContent.Length.ToString(),
      },
    };

    return Task.FromResult(result);
  }
}
