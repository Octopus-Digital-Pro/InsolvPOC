namespace Insolvio.Core.Infrastructure;

/// <summary>
/// Centralised helper for building well-known file-storage key paths.
///
/// Folder structure:
///   cases/{caseId}/
///     {docType}/          ← one folder per document type
///       {docId}{ext}      ← individual files
///     generated/          ← template-generated PDFs
///
/// Both local disk and S3 use the same path convention;
/// <see cref="IFileStorageService.EnsureFolderAsync"/> creates the real
/// directory (local) or a zero-byte marker object (S3) on first use.
/// </summary>
public static class CaseStorageKeys
{
    // ── Document key ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the storage key for a specific document:
    ///   <c>cases/{caseId}/{docType}/{docId}{ext}</c>
    /// </summary>
    public static string Document(Guid caseId, string docType, Guid docId, string ext)
    {
        var safeType = SanitiseSegment(docType);
        var dotExt   = ext.StartsWith('.') ? ext : $".{ext}";
        return $"cases/{caseId}/{safeType}/{docId}{dotExt}";
    }

    // ── Folder keys ─────────────────────────────────────────────────────────

    /// <summary>Returns the folder prefix for one document type under a case.</summary>
    public static string Folder(Guid caseId, string docType)
        => $"cases/{caseId}/{SanitiseSegment(docType)}/";

    /// <summary>Returns the root folder for a case.</summary>
    public static string CaseRoot(Guid caseId)
        => $"cases/{caseId}/";

    // ── Standard folders ────────────────────────────────────────────────────

    /// <summary>
    /// All standard document-type folders created on case initialisation.
    /// </summary>
    public static readonly string[] StandardDocTypes =
    [
        "CourtOpeningDecision",
        "BpiPublication",
        "CreditorNotification",
        "CreditorClaim",
        "AssetInventory",
        "PractitionerReport",
        "FinancialStatement",
        "TaxCertificate",
        "BankStatement",
        "LiquidationReport",
        "Generated",
        "Other",
    ];

    /// <summary>Returns all standard folder prefixes for a newly created case.</summary>
    public static IEnumerable<string> StandardFolders(Guid caseId)
        => StandardDocTypes.Select(t => Folder(caseId, t));

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string SanitiseSegment(string segment)
        => string.Concat(segment.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
}
