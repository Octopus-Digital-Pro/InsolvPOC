namespace Insolvio.Domain;

/// <summary>
/// Domain rules for insolvency document types.
/// </summary>
public static class DocumentTypeRules
{
    /// <summary>Doc types that require a digital signature before submission per legal requirements.</summary>
    private static readonly HashSet<string> SignatureRequiredDocTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "court_opening_decision",
        "notification_opening",
        "report_art_97",
        "claims_table_preliminary",
        "claims_table_definitive",
 "creditors_meeting_minutes",
        "final_report_art_167",
    };

    /// <summary>Determine if a document type requires a digital signature before submission.</summary>
    public static bool RequiresSignature(string? docType)
  => !string.IsNullOrWhiteSpace(docType) && SignatureRequiredDocTypes.Contains(docType);
}
