namespace Insolvex.Core.Abstractions;

/// <summary>
/// Interface for document extraction pipeline.
/// Extracts parties, dates, actions, and structured fields from uploaded documents.
/// </summary>
public interface IDocumentExtractionService
{
    Task<ExtractionResult> ExtractAsync(byte[] fileContent, string fileName, string? existingDocType = null);
}

public class ExtractionResult
{
    public string? DocType { get; set; }
    public int Confidence { get; set; }
    public string? Summary { get; set; }
    public List<ExtractedParty> Parties { get; set; } = new();
    public List<ExtractedDate> Dates { get; set; } = new();
public List<ExtractedAction> Actions { get; set; } = new();
    public Dictionary<string, string> Fields { get; set; } = new();
}

public class ExtractedParty
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Identifier { get; set; }
}

public class ExtractedDate
{
  public DateTime Date { get; set; }
    public string? Meaning { get; set; }
    public string? Source { get; set; }
}

public class ExtractedAction
{
    public string? Action { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Assignee { get; set; }
}
