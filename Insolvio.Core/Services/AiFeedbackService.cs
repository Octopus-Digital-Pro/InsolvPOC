using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class AiFeedbackService : IAiFeedbackService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AiFeedbackService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task RecordCorrectionsAsync(IReadOnlyList<AiCorrectionFeedbackDto> corrections, CancellationToken ct = default)
    {
        if (corrections.Count == 0) return;

        var tenantIdHash = HashTenantId(_currentUser.TenantId);
        var now = DateTime.UtcNow;

        foreach (var c in corrections)
        {
            _db.AiCorrectionFeedbacks.Add(new AiCorrectionFeedback
            {
                Id = Guid.NewGuid(),
                DocumentType = c.DocumentType,
                FieldName = c.FieldName,
                AiSuggestedValue = c.AiSuggestedValue,
                UserCorrectedValue = c.UserCorrectedValue,
                WasAccepted = c.WasAccepted,
                AiConfidence = c.AiConfidence,
                DocumentTextSnippet = Truncate(c.DocumentTextSnippet, 500),
                TenantIdHash = tenantIdHash,
                CorrectedAt = now,
                Source = c.Source,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AiFeedbackStatsDto> GetStatisticsAsync(string? documentType = null, CancellationToken ct = default)
    {
        var query = _db.AiCorrectionFeedbacks.AsNoTracking().AsQueryable();

        // Filter by tenant hash so each tenant sees only their own stats
        var tenantHash = HashTenantId(_currentUser.TenantId);
        query = query.Where(f => f.TenantIdHash == tenantHash);

        if (!string.IsNullOrEmpty(documentType))
            query = query.Where(f => f.DocumentType == documentType);

        var total = await query.CountAsync(ct);
        var accepted = await query.CountAsync(f => f.WasAccepted, ct);
        var corrected = total - accepted;
        var acceptanceRate = total > 0 ? (double)accepted / total : 0;

        var fieldStats = await query
            .GroupBy(f => f.FieldName)
            .Select(g => new FieldAccuracyDto(
                g.Key,
                g.Count(),
                g.Count(f => f.WasAccepted),
                g.Count() > 0 ? (double)g.Count(f => f.WasAccepted) / g.Count() : 0))
            .ToListAsync(ct);

        return new AiFeedbackStatsDto(
            total,
            accepted,
            corrected,
            acceptanceRate,
            fieldStats.ToDictionary(f => f.FieldName));
    }

    private static string HashTenantId(Guid? tenantId)
    {
        if (tenantId is null) return "unknown";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.Value.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];
}
