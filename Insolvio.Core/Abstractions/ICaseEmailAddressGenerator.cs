namespace Insolvio.Core.Abstractions;

public interface ICaseEmailAddressGenerator
{
    /// <summary>
    /// Generate a unique per-case email address from the debtor name and case number.
    /// Format: {sanitized-name}-{num}@insolvio.io (max 10 chars local part).
    /// </summary>
    Task<string> GenerateAsync(string debtorName, string caseNumber, CancellationToken ct = default);
}
