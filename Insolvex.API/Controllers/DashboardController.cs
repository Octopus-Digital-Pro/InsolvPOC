using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Domain.Enums;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.DashboardView)]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
  _currentUser = currentUser;
    }

    [HttpGet]
 public async Task<IActionResult> Get()
    {
var now = DateTime.UtcNow;

    var totalCases = await _db.InsolvencyCases.CountAsync();
        var openCases = await _db.InsolvencyCases
    .CountAsync(c => c.Stage != Domain.Enums.CaseStage.Closure);
        var totalCompanies = await _db.Companies.CountAsync();

     var userId = _currentUser.UserId;
 var pendingTasks = await _db.CompanyTasks
    .CountAsync(t => t.Status == Domain.Enums.TaskStatus.Open &&
   (userId == null || t.AssignedToUserId == userId));
    var overdueTasks = await _db.CompanyTasks
         .CountAsync(t => t.Status != Domain.Enums.TaskStatus.Done &&
     t.Deadline.HasValue && t.Deadline.Value < now &&
  (userId == null || t.AssignedToUserId == userId));

   // Upcoming deadlines from cases
     var upcomingDeadlines = await _db.InsolvencyCases
        .Where(c => c.NextHearingDate.HasValue && c.NextHearingDate > now)
        .OrderBy(c => c.NextHearingDate)
  .Take(10)
           .Select(c => new UpcomingDeadlineDto(
           c.Id,
 c.CaseNumber,
    c.DebtorName,
       "Next Hearing",
      c.NextHearingDate!.Value,
  c.Company != null ? c.Company.Name : null
     ))
            .ToListAsync();

        var claimsDeadlines = await _db.InsolvencyCases
.Where(c => c.ClaimsDeadline.HasValue && c.ClaimsDeadline > now)
     .OrderBy(c => c.ClaimsDeadline)
            .Take(10)
         .Select(c => new UpcomingDeadlineDto(
    c.Id,
  c.CaseNumber,
     c.DebtorName,
 "Claims Deadline",
  c.ClaimsDeadline!.Value,
          c.Company != null ? c.Company.Name : null
            ))
    .ToListAsync();

        var allDeadlines = upcomingDeadlines
            .Concat(claimsDeadlines)
    .OrderBy(d => d.DeadlineDate)
   .Take(15)
      .ToList();

        // Calendar events: hearings + task deadlines
   var calendarEvents = new List<CalendarEventDto>();

 // Case hearings
   var hearings = await _db.InsolvencyCases
     .Where(c => c.NextHearingDate.HasValue &&
    c.NextHearingDate >= now.AddDays(-7) &&
     c.NextHearingDate <= now.AddDays(90))
 .Select(c => new CalendarEventDto(
 c.Id,
     $"Hearing: {c.CaseNumber}",
     c.NextHearingDate!.Value,
     null,
     "hearing",
        c.Id,
     c.CompanyId,
        c.DebtorName
   ))
    .ToListAsync();
        calendarEvents.AddRange(hearings);

        // Task deadlines
      var taskEvents = await _db.CompanyTasks
   .Where(t => t.Deadline.HasValue &&
       t.Status != Domain.Enums.TaskStatus.Done &&
       t.Deadline >= now.AddDays(-7) &&
  t.Deadline <= now.AddDays(90) &&
             (userId == null || t.AssignedToUserId == userId))
            .Include(t => t.Company)
     .Select(t => new CalendarEventDto(
    t.Id,
        t.Title,
          t.Deadline!.Value,
     null,
           "task",
 null,
   t.CompanyId,
      t.Company != null ? t.Company.Name : null
     ))
    .ToListAsync();
      calendarEvents.AddRange(taskEvents);

   // Recent tasks
     var recentTasks = await _db.CompanyTasks
       .Include(t => t.Company)
 .Include(t => t.AssignedTo)
      .Where(t => userId == null || t.AssignedToUserId == userId)
 .OrderBy(t => t.Deadline)
     .Take(20)
      .ToListAsync();

    var dashboard = new DashboardDto(
   totalCases,
  openCases,
  totalCompanies,
         pendingTasks,
     overdueTasks,
     allDeadlines,
     calendarEvents.OrderBy(e => e.Start).ToList(),
      recentTasks.Select(t => t.ToDto()).ToList()
   );

        return Ok(dashboard);
    }
}
