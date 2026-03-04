using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Enums;
using TaskStatus = Insolvex.Domain.Enums.TaskStatus;

namespace Insolvex.Core.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;
        var now = DateTime.UtcNow;

        var casesQuery = _db.InsolvencyCases.Where(c => tenantId == null || c.TenantId == tenantId);
        var tasksQuery = _db.CompanyTasks.Where(t => tenantId == null || t.TenantId == tenantId);

        var totalCases = await casesQuery.CountAsync(ct);
        var openCases = await casesQuery.CountAsync(c => c.Status != "Closed", ct);
        var totalCompanies = await _db.Companies
        .Where(c => tenantId == null || c.TenantId == tenantId).CountAsync(ct);

        var userTasksQuery = tasksQuery;
        if (userId.HasValue)
            userTasksQuery = userTasksQuery.Where(t => t.AssignedToUserId == userId);

        var pendingTasks = await userTasksQuery.CountAsync(t => t.Status == TaskStatus.Open, ct);
        var overdueTasks = await userTasksQuery.CountAsync(t =>
              t.Status != TaskStatus.Done && t.Deadline.HasValue && t.Deadline.Value < now, ct);

        // Upcoming deadlines
        var hearingDeadlines = await casesQuery
            .Where(c => c.NextHearingDate.HasValue && c.NextHearingDate > now)
        .OrderBy(c => c.NextHearingDate).Take(10)
        .Select(c => new UpcomingDeadlineDto(c.Id, c.CaseNumber, c.DebtorName, "Next Hearing",
           c.NextHearingDate!.Value, c.Company != null ? c.Company.Name : null))
   .ToListAsync(ct);

        var claimsDeadlines = await casesQuery
            .Where(c => c.ClaimsDeadline.HasValue && c.ClaimsDeadline > now)
 .OrderBy(c => c.ClaimsDeadline).Take(10)
      .Select(c => new UpcomingDeadlineDto(c.Id, c.CaseNumber, c.DebtorName, "Claims Deadline",
             c.ClaimsDeadline!.Value, c.Company != null ? c.Company.Name : null))
            .ToListAsync(ct);

        var allDeadlines = hearingDeadlines.Concat(claimsDeadlines)
             .OrderBy(d => d.DeadlineDate).Take(15).ToList();

        // Calendar events
        var hearingEvents = await casesQuery
            .Where(c => c.NextHearingDate.HasValue
      && c.NextHearingDate >= now.AddDays(-7) && c.NextHearingDate <= now.AddDays(90))
            .Select(c => new DashboardCalendarItemDto(c.Id, $"Hearing: {c.CaseNumber}",
      c.NextHearingDate!.Value, null, "hearing", c.Id, c.CompanyId, c.DebtorName))
            .ToListAsync(ct);

        var taskEvents = await userTasksQuery
        .Where(t => t.Deadline.HasValue && t.Status != TaskStatus.Done
  && t.Deadline >= now.AddDays(-7) && t.Deadline <= now.AddDays(90))
         .Include(t => t.Company)
       .Select(t => new DashboardCalendarItemDto(t.Id, t.Title,
       t.Deadline!.Value, null, "task", null, t.CompanyId,
         t.Company != null ? t.Company.Name : null))
            .ToListAsync(ct);

        var calendarEvents = hearingEvents.Concat(taskEvents).OrderBy(e => e.Start).ToList();

        // Recent tasks
        var recentTasks = await userTasksQuery
                  .Include(t => t.Company).Include(t => t.AssignedTo)
           .OrderBy(t => t.Deadline).Take(20)
        .ToListAsync(ct);

        return new DashboardDto(totalCases, openCases, totalCompanies,
       pendingTasks, overdueTasks, allDeadlines, calendarEvents,
    recentTasks.Select(t => t.ToDto()).ToList());
    }
}
