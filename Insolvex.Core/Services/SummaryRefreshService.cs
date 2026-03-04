using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Services;

/// <summary>
/// Lightweight service to trigger AI summary refresh after key events.
/// Per InsolvencyAppRules section 6: refresh after document upload + acceptance,
/// task status changes, stage transitions, and optionally daily.
/// </summary>
public class SummaryRefreshService
{
  private readonly ICaseSummaryService _summaryService;
  private readonly IApplicationDbContext _db;
  private readonly ILogger<SummaryRefreshService> _logger;

  public SummaryRefreshService(
      ICaseSummaryService summaryService,
      IApplicationDbContext db,
      ILogger<SummaryRefreshService> logger)
  {
    _summaryService = summaryService;
    _db = db;
    _logger = logger;
  }

  /// <summary>
  /// Refresh the case summary if the last one is stale (older than threshold).
  /// Fire-and-forget safe — catches all exceptions.
  /// </summary>
  public async Task RefreshIfStaleAsync(Guid caseId, Guid tenantId, string trigger, TimeSpan? staleness = null)
  {
    try
    {
      var threshold = staleness ?? TimeSpan.FromMinutes(5);
      var lastSummary = _db.CaseSummaries
    .Where(s => s.CaseId == caseId)
    .OrderByDescending(s => s.GeneratedAt)
    .FirstOrDefault();

      // Skip if recently refreshed
      if (lastSummary != null && (DateTime.UtcNow - lastSummary.GeneratedAt) < threshold)
        return;

      var result = await _summaryService.GenerateAsync(caseId);
      if (result.Error != null)
      {
        _logger.LogWarning("Summary refresh failed for case {CaseId}: {Error}", caseId, result.Error);
        return;
      }

      _db.CaseSummaries.Add(new CaseSummary
      {
        TenantId = tenantId,
        CaseId = caseId,
        Model = "stub-v1",
        SnapshotJson = result.SnapshotJson,
        Text = result.Text,
        TextByLanguageJson = result.TextByLanguage.Count > 0
          ? System.Text.Json.JsonSerializer.Serialize(result.TextByLanguage)
          : null,
        NextActionsJson = System.Text.Json.JsonSerializer.Serialize(result.NextActions),
        RisksJson = System.Text.Json.JsonSerializer.Serialize(result.Risks),
        UpcomingDeadlinesJson = System.Text.Json.JsonSerializer.Serialize(result.UpcomingDeadlines),
        Trigger = trigger,
        GeneratedAt = DateTime.UtcNow,
      });
      await _db.SaveChangesAsync();

      _logger.LogInformation("Auto-refreshed summary for case {CaseId} (trigger: {Trigger})", caseId, trigger);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to auto-refresh summary for case {CaseId}", caseId);
    }
  }
}
