using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;

namespace Insolvex.API.Services;

/// <summary>
/// Stub case summary service generating status narrative from case data.
/// Designed for real LLM integration later.
/// </summary>
public class StubCaseSummaryService : ICaseSummaryService
{
    private readonly ApplicationDbContext _db;

    public StubCaseSummaryService(ApplicationDbContext db) => _db = db;

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

        var text = $@"## Case Status: {c.CaseNumber}
- **Stage:** {c.Stage}
- **Debtor:** {c.DebtorName} (CUI: {c.DebtorCui ?? "N/A"})
- **Court:** {c.CourtName ?? "N/A"}
- **Practitioner:** {c.PractitionerName ?? "Unassigned"}
- **Notice Date:** {c.NoticeDate?.ToString("dd.MM.yyyy") ?? c.OpeningDate?.ToString("dd.MM.yyyy") ?? "Not extracted"}
- **Open Tasks:** {openTasks} ({overdueTasks} overdue)
- **Documents:** {docCount}
- **Parties:** {partyCount}
- **Claims Deadline:** {c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "Not set"}";

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

  var snapshot = new
        {
      caseId, stage = c.Stage.ToString(), openTasks, overdueTasks,
            docCount, partyCount, generatedAt = DateTime.UtcNow,
  };

        return new CaseSummaryResult
      {
        Text = text,
        NextActions = nextActions,
   Risks = risks,
     UpcomingDeadlines = upcomingDeadlines,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
        };
    }
}
