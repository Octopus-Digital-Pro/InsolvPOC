using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Core.Mapping;
using Insolvio.Domain.Entities;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Services;

/// <summary>
/// Stub case summary service generating status narrative from case data.
/// Designed for real LLM integration later.
/// </summary>
public class StubCaseSummaryService : ICaseSummaryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICaseEventService _caseEvents;

    public StubCaseSummaryService(IApplicationDbContext db, ICaseEventService caseEvents)
    {
        _db = db;
        _caseEvents = caseEvents;
    }

    public async Task<CaseSummaryResult> GenerateAsync(Guid caseId)
    {
        var c = await _db.InsolvencyCases
      .Include(x => x.Parties)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == caseId);

        if (c == null)
            return new CaseSummaryResult { Error = "Case not found" };

        var tasks = await _db.CompanyTasks.Where(t => t.CaseId == caseId).ToListAsync();
        var openTasks = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Open);
        var overdueTasks = tasks.Count(t => t.Deadline < DateTime.UtcNow && t.Status != Domain.Enums.TaskStatus.Done);
        var docCount = c.Documents.Count;
        var partyCount = c.Parties.Count;

        var noticeDateText = c.NoticeDate?.ToString("dd.MM.yyyy") ?? c.OpeningDate?.ToString("dd.MM.yyyy") ?? "N/A";
        var claimsDeadlineText = c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "N/A";
        var debtorCuiText = c.DebtorCui ?? "N/A";

        // Append AI event timeline context (last 50 events)
        var eventsContext = await _caseEvents.BuildAiContextAsync(c.Id, 50, CancellationToken.None);
        var textByLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = BuildSummaryEn(c, debtorCuiText, noticeDateText, openTasks, overdueTasks, docCount, partyCount, claimsDeadlineText, eventsContext),
            ["ro"] = BuildSummaryRo(c, debtorCuiText, noticeDateText, openTasks, overdueTasks, docCount, partyCount, claimsDeadlineText, eventsContext),
            ["hu"] = BuildSummaryHu(c, debtorCuiText, noticeDateText, openTasks, overdueTasks, docCount, partyCount, claimsDeadlineText, eventsContext),
        };

        var preferredLanguage = await ResolveTenantLanguageAsync(c.TenantId);
        var selectedText = textByLanguage.TryGetValue(preferredLanguage, out var localized)
            ? localized
            : textByLanguage["en"];

        var nextActions = tasks
         .Where(t => t.Status != Domain.Enums.TaskStatus.Done)
           .OrderBy(t => t.Deadline)
           .Take(5)
              .Select(t => $"{t.Title} (due: {t.Deadline?.ToString("dd.MM.yyyy") ?? "N/A"})")
          .ToList();

        var risks = new List<string>();
        if (overdueTasks > 0) risks.Add($"{overdueTasks} overdue task(s)");
        if (c.Parties.Count == 0) risks.Add("No parties registered");
        if (c.Documents.Count == 0) risks.Add("No documents uploaded");
        if (c.ClaimsDeadline.HasValue && c.ClaimsDeadline.Value < DateTime.UtcNow)
            risks.Add("Claims deadline has passed");

        var upcomingDeadlines = tasks
    .Where(t => t.Deadline.HasValue && t.Deadline.Value > DateTime.UtcNow && t.Status != Domain.Enums.TaskStatus.Done)
   .OrderBy(t => t.Deadline)
     .Take(10)
 .Select(t => new CaseSummaryDeadline
 {
     Title = t.Title,
     Deadline = t.Deadline!.Value,
     IsOverdue = t.Deadline.Value < DateTime.UtcNow,
 })
   .ToList();

        var missedDeadlines = await _db.CaseEvents
            .CountAsync(e => e.CaseId == caseId && e.EventType == "Deadline.Missed");

        var snapshot = new
        {
            caseId,
            stage = c.Status,
            openTasks,
            overdueTasks,
            docCount,
            partyCount,
            missedDeadlines,
            generatedAt = DateTime.UtcNow,
        };

        return new CaseSummaryResult
        {
            Text = selectedText,
            TextByLanguage = textByLanguage,
            NextActions = nextActions,
            Risks = risks,
            UpcomingDeadlines = upcomingDeadlines,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
        };
    }

    public async Task<CaseSummaryDto> GenerateAndSaveAsync(Guid caseId, string? trigger = null)
    {
        var result = await GenerateAsync(caseId);

        var tenantId = await _db.InsolvencyCases
            .Where(c => c.Id == caseId)
            .Select(c => c.TenantId)
            .FirstOrDefaultAsync();

        var entity = new CaseSummary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            Text = result.Text,
            TextByLanguageJson = result.TextByLanguage.Count > 0 ? JsonSerializer.Serialize(result.TextByLanguage) : null,
            NextActionsJson = JsonSerializer.Serialize(result.NextActions),
            RisksJson = JsonSerializer.Serialize(result.Risks),
            UpcomingDeadlinesJson = JsonSerializer.Serialize(result.UpcomingDeadlines),
            Trigger = trigger ?? "Manual",
            Model = "Stub",
            GeneratedAt = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
        };

        _db.CaseSummaries.Add(entity);
        await _db.SaveChangesAsync();

        return entity.ToDto();
    }

    public async Task<CaseSummaryDto?> GetLatestAsync(Guid caseId)
    {
        var entity = await _db.CaseSummaries
            .AsNoTracking()
            .Where(s => s.CaseId == caseId)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefaultAsync();

        return entity?.ToDto();
    }

    public async Task<List<CaseSummaryHistoryItem>> GetHistoryAsync(Guid caseId, int take = 10)
    {
        return await _db.CaseSummaries
            .AsNoTracking()
            .Where(s => s.CaseId == caseId)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(take)
            .Select(s => s.ToHistoryItem())
            .ToListAsync();
    }

    private static string BuildSummaryEn(
        InsolvencyCase c,
        string debtorCuiText,
        string noticeDateText,
        int openTasks,
        int overdueTasks,
        int docCount,
        int partyCount,
        string claimsDeadlineText,
        string eventsContext)
    {
        var text = $@"## Case Status: {c.CaseNumber}
- **Status:** {c.Status}
- **Debtor:** {c.DebtorName} (CUI: {debtorCuiText})
- **Court:** {c.CourtName ?? "N/A"}
- **Practitioner:** {c.PractitionerName ?? "Unassigned"}
- **Notice Date:** {noticeDateText}
- **Open Tasks:** {openTasks} ({overdueTasks} overdue)
- **Documents:** {docCount}
- **Parties:** {partyCount}
- **Claims Deadline:** {claimsDeadlineText}
";

        if (!string.IsNullOrWhiteSpace(eventsContext))
        {
            text += $"""

## Recent Activity Timeline
{eventsContext}
""";
        }

        return text;
    }

    private static string BuildSummaryRo(
        InsolvencyCase c,
        string debtorCuiText,
        string noticeDateText,
        int openTasks,
        int overdueTasks,
        int docCount,
        int partyCount,
        string claimsDeadlineText,
        string eventsContext)
    {
        var text = $@"## Stare dosar: {c.CaseNumber}
- **Status:** {c.Status}
- **Debitor:** {c.DebtorName} (CUI: {debtorCuiText})
- **Instanță:** {c.CourtName ?? "N/A"}
- **Practician:** {c.PractitionerName ?? "Nealocat"}
- **Data notificării:** {noticeDateText}
- **Sarcini deschise:** {openTasks} ({overdueTasks} restante)
- **Documente:** {docCount}
- **Părți:** {partyCount}
- **Termen creanțe:** {claimsDeadlineText}
";

        if (!string.IsNullOrWhiteSpace(eventsContext))
        {
            text += $"""

## Activitate recentă
{eventsContext}
""";
        }

        return text;
    }

    private static string BuildSummaryHu(
        InsolvencyCase c,
        string debtorCuiText,
        string noticeDateText,
        int openTasks,
        int overdueTasks,
        int docCount,
        int partyCount,
        string claimsDeadlineText,
        string eventsContext)
    {
        var text = $@"## Ügy állapota: {c.CaseNumber}
- **Állapot:** {c.Status}
- **Adós:** {c.DebtorName} (CUI: {debtorCuiText})
- **Bíróság:** {c.CourtName ?? "N/A"}
- **Szakértő:** {c.PractitionerName ?? "Nincs hozzárendelve"}
- **Értesítés dátuma:** {noticeDateText}
- **Nyitott feladatok:** {openTasks} ({overdueTasks} lejárt)
- **Dokumentumok:** {docCount}
- **Felek:** {partyCount}
- **Követelési határidő:** {claimsDeadlineText}
";

        if (!string.IsNullOrWhiteSpace(eventsContext))
        {
            text += $"""

## Legutóbbi aktivitás
{eventsContext}
""";
        }

        return text;
    }

    private async Task<string> ResolveTenantLanguageAsync(Guid tenantId)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Language, t.Region })
            .FirstOrDefaultAsync();

        var language = tenant?.Language?.ToLowerInvariant();
        if (language is "ro" or "hu" or "en")
            return language;

        return tenant?.Region switch
        {
            SystemRegion.Romania => "ro",
            SystemRegion.Hungary => "hu",
            _ => "en",
        };
    }
}
