namespace Insolvex.Core.Configuration;

/// <summary>
/// Configuration for the mail merge / document generation system.
/// </summary>
public class MailMergeOptions
{
    public const string SectionName = "MailMerge";

    /// <summary>Path to the templates folder (relative to ContentRootPath).</summary>
    public string TemplatesPath { get; set; } = "Templates-Ro";

    /// <summary>Path to the generated document output folder (relative to ContentRootPath).</summary>
    public string OutputPath { get; set; } = "DocumentOutput";
}
