using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Entities;

namespace Insolvex.Core.Services;

/// <summary>
/// Records and queries the immutable case event timeline.
/// Every action in the system that touches a case should call one of the
/// Record* methods here. The events are then used to build AI context.
/// </summary>
public sealed class CaseEventService : ICaseEventService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public CaseEventService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ── Generic record ─────────────────────────────────────────────────────

    public async Task<CaseEventDto> RecordAsync(Guid caseId, RecordCaseEventRequest req, CancellationToken ct = default)
    {
        var actorName = await ResolveActorNameAsync();

        var ev = new CaseEvent
        {
            TenantId = _currentUser.TenantId ?? Guid.Empty,
            CaseId = caseId,
            Category = req.Category,
            EventType = req.EventType,
            Description = req.Description,
            OccurredAt = req.OccurredAt ?? DateTime.UtcNow,
            ActorUserId = _currentUser.UserId,
            ActorName = actorName,
            LinkedEntityType = req.LinkedEntityType,
            LinkedEntityId = req.LinkedEntityId,
            InvolvedPartiesJson = SerializeIfNotNull(req.InvolvedParties),
            KeyDatesJson = SerializeIfNotNull(req.KeyDates),
            DocumentSummary = req.DocumentSummary,
            ExtractedActionsJson = SerializeIfNotNull(req.ExtractedActions),
            Severity = req.Severity,
            MetadataJson = SerializeIfNotNull(req.Metadata),
        };

        _db.CaseEvents.Add(ev);
        await _db.SaveChangesAsync(ct);

        return ToDto(ev);
    }

    // ── Convenience helpers ────────────────────────────────────────────────

    public async Task<CaseEventDto> RecordDocumentUploadedAsync(Guid caseId, Guid documentId,
        string fileName, string docType, string? aiSummary,
        object? extractedParties, object? extractedDates, object? extractedActions,
        CancellationToken ct = default)
    {
        return await RecordAsync(caseId, new RecordCaseEventRequest
        {
            Category = "Document",
            EventType = "Document.Uploaded",
            Description = $"Document uploaded: {fileName} ({docType})" +
                          (aiSummary != null ? $" — {TruncateForDisplay(aiSummary, 200)}" : ""),
            LinkedEntityType = "InsolvencyDocument",
            LinkedEntityId = documentId,
            InvolvedParties = extractedParties,
            KeyDates = extractedDates,
            DocumentSummary = aiSummary,
            ExtractedActions = extractedActions,
            Severity = "Info",
            Metadata = new { fileName, docType, hasAiSummary = aiSummary != null },
        }, ct);
    }

    public async Task<CaseEventDto> RecordTaskEventAsync(Guid caseId, Guid taskId,
        string eventType, string taskTitle, string? description,
        object? involvedParties, DateTime? deadline,
        CancellationToken ct = default)
    {
        var isMissed = deadline.HasValue && deadline.Value < DateTime.UtcNow && eventType == "Task.Completed";
        var severity = eventType == "Task.Overdue" ? "Warning"
                     : eventType == "Task.Blocked" ? "Warning"
                     : "Info";

        return await RecordAsync(caseId, new RecordCaseEventRequest
        {
            Category = "Task",
            EventType = eventType,
            Description = description ?? $"Task \"{taskTitle}\" — {HumanizeEventType(eventType)}" +
                          (deadline.HasValue ? $" (deadline: {deadline.Value:dd.MM.yyyy})" : ""),
            LinkedEntityType = "CompanyTask",
            LinkedEntityId = taskId,
            InvolvedParties = involvedParties,
            KeyDates = deadline.HasValue
                ? new[] { new { date = deadline.Value.ToString("yyyy-MM-dd"), meaning = "Task deadline" } }
                : null,
            Severity = severity,
            Metadata = new { taskTitle, deadline, isMissed },
        }, ct);
    }

    public async Task<CaseEventDto> RecordDeadlineEventAsync(Guid caseId,
        string deadlineName, DateTime deadlineDate, bool isMissed,
        Guid? linkedEntityId, string? linkedEntityType,
        object? involvedParties, CancellationToken ct = default)
    {
        return await RecordAsync(caseId, new RecordCaseEventRequest
        {
            Category = "Deadline",
            EventType = isMissed ? "Deadline.Missed" : "Deadline.Upcoming",
            Description = isMissed
                ? $"MISSED DEADLINE: {deadlineName} was due {deadlineDate:dd.MM.yyyy}"
                : $"Deadline approaching: {deadlineName} on {deadlineDate:dd.MM.yyyy}",
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            InvolvedParties = involvedParties,
            KeyDates = new[] { new { date = deadlineDate.ToString("yyyy-MM-dd"), meaning = deadlineName } },
            Severity = isMissed ? "Critical" : "Warning",
            Metadata = new { deadlineName, deadlineDate, isMissed },
        }, ct);
    }

    // ── Query ──────────────────────────────────────────────────────────────

    public async Task<CaseEventsPageDto> GetByCaseAsync(Guid caseId, int page = 1, int pageSize = 50,
        string? category = null, CancellationToken ct = default)
    {
        var q = _db.CaseEvents.Where(e => e.CaseId == caseId).AsQueryable();

        if (!string.IsNullOrEmpty(category))
            q = q.Where(e => e.Category == category);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => ToDto(e))
            .ToListAsync(ct);

        return new CaseEventsPageDto(items, totalCount, page, pageSize);
    }

    /// <summary>
    /// Builds a condensed, token-efficient string of the most recent case events
    /// suitable for injection into an AI prompt.
    /// Format: one line per event — "[date] [Category] [EventType]: [Description] | Parties: ... | Dates: ..."
    /// </summary>
    public async Task<string> BuildAiContextAsync(Guid caseId, int maxEvents = 100, CancellationToken ct = default)
    {
        var events = await _db.CaseEvents
            .Where(e => e.CaseId == caseId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(maxEvents)
            .ToListAsync(ct);

        if (events.Count == 0)
            return "(No case events recorded yet.)";

        var sb = new StringBuilder();
        sb.AppendLine("=== CASE EVENT TIMELINE (newest first) ===");

        foreach (var ev in events)
        {
            sb.Append($"[{ev.OccurredAt:yyyy-MM-dd HH:mm}] [{ev.Category}] {ev.EventType}: {ev.Description}");

            if (ev.ActorName != null)
                sb.Append($" | By: {ev.ActorName}");

            if (ev.InvolvedPartiesJson != null)
            {
                try
                {
                    var parties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(ev.InvolvedPartiesJson);
                    if (parties?.Count > 0)
                    {
                        var names = parties.Select(p => p.GetValueOrDefault("name", "?") + " (" + p.GetValueOrDefault("role", "?") + ")");
                        sb.Append($" | Parties: {string.Join(", ", names)}");
                    }
                }
                catch { /* skip malformed JSON */ }
            }

            if (ev.KeyDatesJson != null)
            {
                try
                {
                    var dates = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(ev.KeyDatesJson);
                    if (dates?.Count > 0)
                    {
                        var dateStrs = dates.Select(d => d.GetValueOrDefault("meaning", "date") + ": " + d.GetValueOrDefault("date", "?"));
                        sb.Append($" | Dates: {string.Join(", ", dateStrs)}");
                    }
                }
                catch { /* skip */ }
            }

            if (ev.DocumentSummary != null)
                sb.Append($" | DocSummary: {TruncateForDisplay(ev.DocumentSummary, 150)}");

            if (ev.Severity != "Info")
                sb.Append($" [⚠ {ev.Severity.ToUpperInvariant()}]");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<string?> ResolveActorNameAsync()
    {
        if (_currentUser.UserId == null) return "System";
        try
        {
            return await _db.Users.AsNoTracking()
                .Where(u => u.Id == _currentUser.UserId.Value)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync();
        }
        catch { return _currentUser.Email; }
    }

    private static CaseEventDto ToDto(CaseEvent ev) => new(
        ev.Id, ev.CaseId, ev.Category, ev.EventType, ev.Description,
        ev.OccurredAt, ev.ActorUserId, ev.ActorName,
        ev.LinkedEntityType, ev.LinkedEntityId,
        ev.InvolvedPartiesJson, ev.KeyDatesJson,
        ev.DocumentSummary, ev.ExtractedActionsJson,
        ev.Severity, ev.MetadataJson
    );

    private static string? SerializeIfNotNull(object? value) =>
        value == null ? null : JsonSerializer.Serialize(value, JsonOpts);

    private static string TruncateForDisplay(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    private static string HumanizeEventType(string eventType) =>
        eventType.Replace(".", " ").Replace("_", " ").ToLowerInvariant();
}
