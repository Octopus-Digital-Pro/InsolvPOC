using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;

namespace Insolvio.Core.Services;

public sealed partial class CaseEmailAddressGenerator : ICaseEmailAddressGenerator
{
    private const string Domain = "insolvio.io";
    private const int MaxLocalPart = 10;

    private static readonly string[] CompanySuffixes =
        ["srl", "sa", "sca", "scs", "snc", "pfa", "ii", "if", "llc", "ltd", "gmbh", "ag", "corp", "inc", "co"];

    private readonly IApplicationDbContext _db;

    public CaseEmailAddressGenerator(IApplicationDbContext db) => _db = db;

    public async Task<string> GenerateAsync(string debtorName, string caseNumber, CancellationToken ct = default)
    {
        var nameSlug = Sanitize(debtorName);
        var numSuffix = ExtractNumericSuffix(caseNumber);

        // Build candidate: {nameSlug}-{num}, max 10 chars total local part
        var maxNameLen = MaxLocalPart - 1 - numSuffix.Length; // 1 for the hyphen
        if (maxNameLen < 2) maxNameLen = 2; // guarantee at least 2 chars of name

        var prefix = nameSlug.Length > maxNameLen ? nameSlug[..maxNameLen] : nameSlug;
        var candidate = $"{prefix}-{numSuffix}";

        // Truncate if still over limit
        if (candidate.Length > MaxLocalPart)
            candidate = candidate[..MaxLocalPart];

        var baseCandidate = candidate;
        var email = $"{candidate}@{Domain}";

        // Check uniqueness
        if (!await ExistsAsync(email, ct))
            return email;

        // Collision — append a, b, c...
        for (var i = 0; i < 26; i++)
        {
            var suffix = (char)('a' + i);
            candidate = baseCandidate.Length >= MaxLocalPart
                ? baseCandidate[..(MaxLocalPart - 1)] + suffix
                : baseCandidate + suffix;

            if (candidate.Length > MaxLocalPart)
                candidate = candidate[..MaxLocalPart];

            email = $"{candidate}@{Domain}";
            if (!await ExistsAsync(email, ct))
                return email;
        }

        // Extreme fallback — use GUID fragment
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{guid[..2]}-{guid[2..8]}@{Domain}";
    }

    private async Task<bool> ExistsAsync(string email, CancellationToken ct)
        => await _db.InsolvencyCases.AnyAsync(c => c.CaseEmailAddress == email, ct);

    /// <summary>
    /// Remove diacritics, company suffixes, special chars. Produce a lowercase alphanumeric slug.
    /// </summary>
    internal static string Sanitize(string name)
    {
        // Remove diacritics
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Remove company suffixes
        foreach (var suffix in CompanySuffixes)
        {
            clean = SuffixRegex(suffix).Replace(clean, "");
        }

        // Keep only letters and digits
        clean = NonAlphanumericRegex().Replace(clean, "");

        return clean.Length == 0 ? "case" : clean;
    }

    /// <summary>
    /// Extract a short numeric identifier from caseNumber (e.g. "1234/F/2023" → "1234").
    /// </summary>
    internal static string ExtractNumericSuffix(string caseNumber)
    {
        var match = LeadingDigitsRegex().Match(caseNumber);
        return match.Success ? match.Value : "0";
    }

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingDigitsRegex();

    private static Regex SuffixRegex(string suffix)
        => new($@"\b{Regex.Escape(suffix)}\b", RegexOptions.IgnoreCase);
}
