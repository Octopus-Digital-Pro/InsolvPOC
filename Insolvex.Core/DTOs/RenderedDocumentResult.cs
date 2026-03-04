namespace Insolvex.Core.DTOs;

/// <summary>Result of rendering a template document (mail-merge or HTML→PDF).</summary>
public class RenderedDocumentResult
{
    public bool Success { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? MergeDataJson { get; set; }
    /// <summary>SHA-256 hex digest of the rendered file bytes (lowercase).</summary>
    public string? FileHash { get; set; }
    public string? Error { get; set; }
}
