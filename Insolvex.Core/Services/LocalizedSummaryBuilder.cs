using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Core.Services;

internal static class LocalizedSummaryBuilder
{
    public static Dictionary<string, string> BuildDocumentSummaryByLanguage(
        string fileName,
        string docType,
        string? caseNumber,
        string? debtorName)
    {
        var caseSuffixEn = string.IsNullOrWhiteSpace(caseNumber) ? string.Empty : $" for case {caseNumber}";
        var caseSuffixRo = string.IsNullOrWhiteSpace(caseNumber) ? string.Empty : $" pentru dosarul {caseNumber}";
        var caseSuffixHu = string.IsNullOrWhiteSpace(caseNumber) ? string.Empty : $" a {caseNumber} ügyszámú ügyhöz";

        var debtorSuffixEn = string.IsNullOrWhiteSpace(debtorName) ? string.Empty : $" Debtor: {debtorName}.";
        var debtorSuffixRo = string.IsNullOrWhiteSpace(debtorName) ? string.Empty : $" Debitor: {debtorName}.";
        var debtorSuffixHu = string.IsNullOrWhiteSpace(debtorName) ? string.Empty : $" Adós: {debtorName}.";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = $"Document '{fileName}' (type: {docType}) uploaded{caseSuffixEn}.{debtorSuffixEn}".Trim(),
            ["ro"] = $"Documentul '{fileName}' (tip: {docType}) a fost încărcat{caseSuffixRo}.{debtorSuffixRo}".Trim(),
            ["hu"] = $"A(z) '{fileName}' dokumentum (típus: {docType}) feltöltve{caseSuffixHu}.{debtorSuffixHu}".Trim(),
        };
    }

    public static Dictionary<string, string> BuildTaskSummaryByLanguage(
        string title,
        string? description,
        string? category,
        DateTime? deadline,
        TaskStatus status)
    {
        var categoryEn = string.IsNullOrWhiteSpace(category) ? "General" : category;
        var categoryRo = string.IsNullOrWhiteSpace(category) ? "General" : category;
        var categoryHu = string.IsNullOrWhiteSpace(category) ? "Általános" : category;

        var deadlineEn = deadline.HasValue ? $" Deadline: {deadline.Value:dd MMM yyyy}." : string.Empty;
        var deadlineRo = deadline.HasValue ? $" Termen: {deadline.Value:dd MMM yyyy}." : string.Empty;
        var deadlineHu = deadline.HasValue ? $" Határidő: {deadline.Value:dd MMM yyyy}." : string.Empty;

        var descriptionEn = string.IsNullOrWhiteSpace(description) ? string.Empty : $" Details: {description}";
        var descriptionRo = string.IsNullOrWhiteSpace(description) ? string.Empty : $" Detalii: {description}";
        var descriptionHu = string.IsNullOrWhiteSpace(description) ? string.Empty : $" Részletek: {description}";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = $"Task '{title}' [{categoryEn}] is {status}.{deadlineEn}{descriptionEn}".Trim(),
            ["ro"] = $"Sarcina '{title}' [{categoryRo}] este în starea {status}.{deadlineRo}{descriptionRo}".Trim(),
            ["hu"] = $"A(z) '{title}' feladat [{categoryHu}] állapota: {status}.{deadlineHu}{descriptionHu}".Trim(),
        };
    }
}
