namespace Insolvex.Core.DTOs;

/// <summary>
/// Dashboard summary returned to the frontend home page.
/// </summary>
public record DashboardDto(
    int TotalCases,
    int OpenCases,
    int TotalCompanies,
    int PendingTasks,
    int OverdueTasks,
    List<UpcomingDeadlineDto> UpcomingDeadlines,
    List<CalendarEventDto> CalendarEvents,
 List<TaskDto> RecentTasks
);

public record UpcomingDeadlineDto(
Guid CaseId,
    string CaseNumber,
    string DebtorName,
    string DeadlineType,
    DateTime DeadlineDate,
    string? CompanyName
);

public record CalendarEventDto(
    Guid Id,
  string Title,
    DateTime Start,
    DateTime? End,
    string Type,
    Guid? CaseId,
    Guid? CompanyId,
    string? Metadata
);

public record ErrorResponse(string Message, string? Type);
