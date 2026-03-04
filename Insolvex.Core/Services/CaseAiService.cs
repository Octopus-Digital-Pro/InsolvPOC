using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.Core.Services;

/// <summary>
/// AI-powered case summary and chat service.
/// Builds rich case context, calls the configured AI provider,
/// and persists chat history against the case.
/// Falls back gracefully to stub summary when AI is unavailable.
/// </summary>
public sealed class CaseAiService : ICaseAiService
{
    private readonly IApplicationDbContext _db;
    private readonly IAiConfigService _aiConfig;
    private readonly ITenantAiConfigService _tenantAiConfig;
    private readonly ICurrentUserService _currentUser;
    private readonly ICaseSummaryService _stubSummary;   // fallback
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CaseAiService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public CaseAiService(
        IApplicationDbContext db,
        IAiConfigService aiConfig,
        ITenantAiConfigService tenantAiConfig,
        ICurrentUserService currentUser,
        ICaseSummaryService stubSummary,
        IHttpClientFactory http,
        ILogger<CaseAiService> logger)
    {
        _db = db;
        _aiConfig = aiConfig;
        _tenantAiConfig = tenantAiConfig;
        _currentUser = currentUser;
        _stubSummary = stubSummary;
        _http = http;
        _logger = logger;
    }

    // ── Summary ────────────────────────────────────────────────────────────────

    public async Task<CaseSummaryDto> GenerateSummaryAsync(Guid caseId, string language = "ro", CancellationToken ct = default)
    {
        var tenantId = await GetCaseTenantIdAsync(caseId, ct);

        var config = await _aiConfig.GetAsync(ct);
        var tenantCfg = await _tenantAiConfig.GetAsync(tenantId, ct);

        // If AI is off at tenant level, fall back to stub
        if (!tenantCfg.AiEnabled || !tenantCfg.SummaryEnabled)
            return await _stubSummary.GenerateAndSaveAsync(caseId, "manual");

        // Resolve API key: tenant's own key takes precedence over the global key
        var tenantApiKey = await _tenantAiConfig.GetDecryptedApiKeyAsync(tenantId, ct);
        var apiKey = tenantApiKey;
        AiConfigDto effectiveConfig = config;

        if (!string.IsNullOrWhiteSpace(tenantApiKey))
        {
            // Tenant has their own key — build an effective config merging tenant overrides
            effectiveConfig = config with
            {
                Provider = tenantCfg.Provider ?? config.Provider,
                ApiEndpoint = tenantCfg.ApiEndpoint ?? config.ApiEndpoint,
                ModelName = tenantCfg.ModelName ?? config.ModelName,
            };
        }
        else
        {
            // Fall back to global key; require global AI to be enabled
            if (!config.IsEnabled) return await _stubSummary.GenerateAndSaveAsync(caseId, "manual");
            apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            return await _stubSummary.GenerateAndSaveAsync(caseId, "manual");

        try
        {
            var ctx = await BuildCaseContextAsync(caseId, tenantCfg.SummaryActivityDays, ct);
            var systemPrompt = BuildSummarySystemPrompt(language);
            var userPrompt = BuildSummaryUserPrompt(ctx, language);

            var (responseText, tokensUsed) = await CallAiAsync(effectiveConfig, apiKey, systemPrompt, userPrompt, 3000, ct);

            if (responseText is null)
            {
                return await _stubSummary.GenerateAndSaveAsync(caseId, "manual");
            }

            await _tenantAiConfig.RecordTokenUsageAsync(tenantId, tokensUsed, ct);

            // Build multi-language set (generate once for requested language; stub provides others)
            var stubResult = await _stubSummary.GenerateAsync(caseId);
            var textByLanguage = stubResult.TextByLanguage;
            textByLanguage[language] = responseText;

            var entity = new CaseSummary
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CaseId = caseId,
                Text = responseText,
                TextByLanguageJson = JsonSerializer.Serialize(textByLanguage),
                NextActionsJson = JsonSerializer.Serialize(stubResult.NextActions),
                RisksJson = JsonSerializer.Serialize(stubResult.Risks),
                UpcomingDeadlinesJson = JsonSerializer.Serialize(stubResult.UpcomingDeadlines),
                Trigger = "manual-ai",
                Model = effectiveConfig.ModelName ?? effectiveConfig.Provider,
                GeneratedAt = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
            };
            _db.CaseSummaries.Add(entity);
            await _db.SaveChangesAsync(ct);

            return entity.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI summary generation failed for case {CaseId} — falling back to stub", caseId);
            return await _stubSummary.GenerateAndSaveAsync(caseId, "manual");
        }
    }

    public async Task<CaseSummaryDto?> GetLatestSummaryAsync(Guid caseId, CancellationToken ct = default)
        => await _stubSummary.GetLatestAsync(caseId);

    // ── Chat ───────────────────────────────────────────────────────────────────

    public async Task<AiChatResponse> ChatAsync(Guid caseId, AiChatRequest request, CancellationToken ct = default)
    {
        var tenantId = await GetCaseTenantIdAsync(caseId, ct);

        var config = await _aiConfig.GetAsync(ct);
        var tenantCfg = await _tenantAiConfig.GetAsync(tenantId, ct);

        if (!tenantCfg.AiEnabled || !tenantCfg.ChatEnabled)
            throw new InvalidOperationException("AI chat is not enabled for this tenant.");

        // Resolve API key: tenant's own key takes precedence
        var tenantApiKey = await _tenantAiConfig.GetDecryptedApiKeyAsync(tenantId, ct);
        var apiKey = tenantApiKey;
        AiConfigDto effectiveChatConfig = config;

        if (!string.IsNullOrWhiteSpace(tenantApiKey))
        {
            effectiveChatConfig = config with
            {
                Provider = tenantCfg.Provider ?? config.Provider,
                ApiEndpoint = tenantCfg.ApiEndpoint ?? config.ApiEndpoint,
                ModelName = tenantCfg.ModelName ?? config.ModelName,
            };
        }
        else
        {
            if (!config.IsEnabled)
                throw new InvalidOperationException("AI is not enabled.");
            apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI provider is not configured.");

        // Check token limits
        if (tenantCfg.MonthlyTokenLimit > 0 && tenantCfg.CurrentMonthTokensUsed >= tenantCfg.MonthlyTokenLimit)
            throw new InvalidOperationException("Monthly AI token limit reached. Please contact your administrator.");

        var ctx = await BuildCaseContextAsync(caseId, tenantCfg.SummaryActivityDays, ct);
        var systemPrompt = BuildChatSystemPrompt(ctx, request.Language);

        // Load conversation history (last 20 turns to keep within context)
        var history = await _db.AiChatMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.CaseId == caseId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(40) // Last 20 pairs
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var messages = BuildChatMessages(systemPrompt, history, request.Message);

        var (responseText, tokensUsed) = await CallAiChatAsync(effectiveChatConfig, apiKey, messages, 2000, ct);

        if (responseText is null)
            throw new InvalidOperationException("AI provider returned no response. Please try again.");

        await _tenantAiConfig.RecordTokenUsageAsync(tenantId, tokensUsed, ct);

        // Persist user message
        var userId = _currentUser.UserId;
        var userName = _currentUser.Email;

        var userMsg = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            Role = "user",
            Content = request.Message,
            TokensUsed = 0,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            CreatedOn = DateTime.UtcNow,
        };
        _db.AiChatMessages.Add(userMsg);

        // Persist assistant response
        var assistantMsg = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseId = caseId,
            Role = "assistant",
            Content = responseText,
            TokensUsed = tokensUsed,
            Model = config.ModelName ?? config.Provider,
            CreatedAt = DateTime.UtcNow.AddMilliseconds(1),
            CreatedOn = DateTime.UtcNow,
        };
        _db.AiChatMessages.Add(assistantMsg);
        await _db.SaveChangesAsync(ct);

        return new AiChatResponse(
            ToDto(userMsg, userName),
            ToDto(assistantMsg, null),
            tokensUsed
        );
    }

    public async Task<List<AiChatMessageDto>> GetChatHistoryAsync(Guid caseId, int take = 50, CancellationToken ct = default)
    {
        return await _db.AiChatMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.CaseId == caseId)
            .OrderBy(m => m.CreatedAt)
            .Take(take)
            .Select(m => new AiChatMessageDto(
                m.Id,
                m.Role,
                m.Content,
                m.TokensUsed,
                m.Model,
                m.CreatedAt,
                m.UserId,
                m.User != null ? m.User.FirstName + " " + m.User.LastName : null
            ))
            .ToListAsync(ct);
    }

    public async Task ClearChatHistoryAsync(Guid caseId, CancellationToken ct = default)
    {
        var messages = await _db.AiChatMessages
            .IgnoreQueryFilters()
            .Where(m => m.CaseId == caseId)
            .ToListAsync(ct);
        _db.AiChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync(ct);
    }

    // ── Context builder ────────────────────────────────────────────────────────

    private async Task<CaseAiContext> BuildCaseContextAsync(Guid caseId, int activityDays, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-activityDays);

        var c = await _db.InsolvencyCases
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(x => x.Parties)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new KeyNotFoundException($"Case {caseId} not found");

        var assets = await _db.Assets
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.CaseId == caseId)
            .Select(a => new { a.Description, a.AssetType, a.EstimatedValue, a.Status })
            .ToListAsync(ct);

        var creditors = await _db.CaseParties
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.CaseId == caseId && (
                p.Role == CasePartyRole.SecuredCreditor ||
                p.Role == CasePartyRole.UnsecuredCreditor ||
                p.Role == CasePartyRole.BudgetaryCreditor ||
                p.Role == CasePartyRole.EmployeeCreditor))
            .Select(p => new { p.Name, p.Role, p.ClaimAmountRon, p.ClaimAccepted, p.RoleDescription })
            .ToListAsync(ct);

        var tasks = await _db.CompanyTasks
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.CaseId == caseId && (t.CreatedOn >= cutoff || (t.Status != Domain.Enums.TaskStatus.Done)))
            .OrderBy(t => t.Deadline)
            .Select(t => new { t.Title, t.Status, t.Deadline, t.Category })
            .ToListAsync(ct);

        var taskNotes = await _db.TaskNotes
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(n => n.Task != null && n.Task.CaseId == caseId && n.CreatedOn >= cutoff)
            .OrderByDescending(n => n.CreatedOn)
            .Take(30)
            .Select(n => new { n.Content, n.CreatedOn, TaskTitle = n.Task!.Title })
            .ToListAsync(ct);

        var events = await _db.CaseEvents
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.CaseId == caseId && e.CreatedOn >= cutoff)
            .OrderByDescending(e => e.CreatedOn)
            .Take(60)
            .Select(e => new { e.EventType, e.Description, e.CreatedOn })
            .ToListAsync(ct);

        var upcomingCalendar = await _db.CalendarEvents
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.CaseId == caseId && e.Start >= DateTime.UtcNow && !e.IsCancelled)
            .OrderBy(e => e.Start)
            .Take(10)
            .Select(e => new { e.Title, e.EventType, e.Start, e.Location })
            .ToListAsync(ct);

        var stages = await _db.CaseWorkflowStages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.CaseId == caseId)
            .Select(s => new { s.StageKey, s.Status, s.CompletedAt })
            .ToListAsync(ct);

        var upcomingDeadlines = await _db.CaseDeadlines
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.CaseId == caseId && d.DueDate >= DateTime.UtcNow && !d.IsCompleted)
            .OrderBy(d => d.DueDate)
            .Take(10)
            .Select(d => new { d.Label, d.DueDate })
            .ToListAsync(ct);

        return new CaseAiContext
        {
            Case = c,
            Assets = assets.Select(a => $"{a.Description} ({a.AssetType}) = {a.EstimatedValue?.ToString("N0") ?? "unknown"} RON — {a.Status}").ToList(),
            Creditors = creditors.Select(c2 => $"{c2.Name ?? c2.Role.ToString()} [{c2.Role}]: {c2.ClaimAmountRon?.ToString("N0") ?? "?"} RON ({(c2.ClaimAccepted == true ? "accepted" : c2.ClaimAccepted == false ? "rejected" : "pending")})").ToList(),
            TotalCreditorValue = creditors.Sum(c2 => c2.ClaimAmountRon ?? 0),
            OpenTasks = tasks.Where(t => t.Status != Domain.Enums.TaskStatus.Done).Select(t => $"[{t.Category ?? "Task"}] {t.Title} — due {t.Deadline?.ToString("dd.MM.yyyy") ?? "N/A"}").ToList(),
            RecentTaskNotes = taskNotes.Select(n => $"{n.CreatedOn:dd.MM.yyyy} on '{n.TaskTitle}': {n.Content}").ToList(),
            RecentEvents = events.Select(e => $"{e.CreatedOn:dd.MM.yyyy} [{e.EventType}] {e.Description}").ToList(),
            UpcomingCalendarEvents = upcomingCalendar.Select(e => $"{e.Start:dd.MM.yyyy HH:mm} — {e.Title} ({e.EventType}){(e.Location != null ? " @ " + e.Location : "")}").ToList(),
            WorkflowStages = stages.Select(s => $"{s.StageKey}: {s.Status}{(s.CompletedAt.HasValue ? $" (completed {s.CompletedAt:dd.MM.yyyy})" : "")}").ToList(),
            UpcomingDeadlines = upcomingDeadlines.Select(d => $"{d.DueDate:dd.MM.yyyy} — {d.Label}").ToList(),
        };
    }

    private sealed class CaseAiContext
    {
        public required InsolvencyCase Case { get; init; }
        public List<string> Assets { get; init; } = new();
        public List<string> Creditors { get; init; } = new();
        public decimal TotalCreditorValue { get; init; }
        public List<string> OpenTasks { get; init; } = new();
        public List<string> RecentTaskNotes { get; init; } = new();
        public List<string> RecentEvents { get; init; } = new();
        public List<string> UpcomingCalendarEvents { get; init; } = new();
        public List<string> WorkflowStages { get; init; } = new();
        public List<string> UpcomingDeadlines { get; init; } = new();
    }

    // ── Prompt builders ────────────────────────────────────────────────────────

    private static string BuildSummarySystemPrompt(string language) => language switch
    {
        "ro" => """
            Ești un asistent juridic specializat în proceduri de insolvență din România,
            guvernate de Legea nr. 85/2014 privind procedurile de prevenire a insolvenței
            și de insolvență (Legea insolvenței). Ești familiarizat cu procedurile de
            reorganizare judiciară, faliment, lichidare, planul de reorganizare, tabelul
            creanțelor, adunarea creditorilor, raportul administratorului/lichidatorului
            judiciar și toate termenele legale prevăzute.

            Rolul tău este să generezi un rezumat CONCIS și ACȚIONABIL al situației
            dosarului de insolvență în limba română. Rezumatul trebuie să:
            - Descrie stadiul curent al procedurii (în ce fază se află și ce s-a petrecut recent)
            - Evidențieze riscurile și problemele urgente
            - Listeze acțiunile necesare imediat
            - Menționeze termene și ședințe importante
            - Fie redat în limbaj juridic profesional dar ușor de înțeles

            Răspunde EXCLUSIV în limba română, fără introduceri sau concluzii inutile.
            Formatează cu markdown (titluri ##, liste bullet -, bold **text**).
            """,

        "hu" => """
            Ön egy romániai fizetésképtelenségi eljárásokra specializált jogi asszisztens,
            aki ismeri a 2014. évi 85. törvényt (a fizetésképtelenségi törvényt),
            a bírósági reorganizációt, a felszámolást, a hitelezői gyűlést és a vonatkozó
            jogi határidőket.

            Feladata: tömör és cselekvésre ösztönző összefoglaló készítése a fizetésképtelenségi
            ügy aktuális állásáról magyar nyelven.
            Formázd markdown segítségével (## fejlécek, - bullet listák, **félkövér**).
            """,

        _ => """
            You are a legal assistant specialised in Romanian insolvency procedures
            governed by Law no. 85/2014 on insolvency prevention and insolvency proceedings.
            You understand judicial reorganisation, bankruptcy, liquidation, creditor meetings,
            claims tables, the administrator/liquidator reports, and all statutory deadlines.

            Your task: Generate a CONCISE, ACTIONABLE assessment of the insolvency case's
            current status. Cover:
            - Current procedure phase and recent developments
            - Urgent risks and issues requiring attention
            - Immediate action items
            - Upcoming deadlines and hearings

            Respond in professional English. Format with markdown (## headings, - bullets, **bold**).
            """,
    };

    private static string BuildSummaryUserPrompt(CaseAiContext ctx, string language)
    {
        var c = ctx.Case;
        var sb = new StringBuilder();

        sb.AppendLine("## INSOLVENCY CASE DATA");
        sb.AppendLine($"- Case Number: {c.CaseNumber}");
        sb.AppendLine($"- Debtor: {c.DebtorName} (CUI: {c.DebtorCui ?? "N/A"})");
        sb.AppendLine($"- Court: {c.CourtName ?? "N/A"}");
        sb.AppendLine($"- Status / Phase: {c.Status}");
        sb.AppendLine($"- Procedure Type: {c.ProcedureType}");
        sb.AppendLine($"- Practitioner: {c.PractitionerName ?? "Unassigned"}");
        sb.AppendLine($"- Opening Date: {c.OpeningDate?.ToString("dd.MM.yyyy") ?? "N/A"}");
        sb.AppendLine($"- Claims Deadline: {c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "N/A"}");

        if (c.TotalClaimsRon.HasValue)
        {
            sb.AppendLine($"- Total Claims: {c.TotalClaimsRon:N0} RON");
            sb.AppendLine($"  - Secured: {c.SecuredClaimsRon?.ToString("N0") ?? "0"} RON");
            sb.AppendLine($"  - Budgetary: {c.BudgetaryClaimsRon?.ToString("N0") ?? "0"} RON");
            sb.AppendLine($"  - Employee: {c.EmployeeClaimsRon?.ToString("N0") ?? "0"} RON");
            sb.AppendLine($"  - Unsecured: {c.UnsecuredClaimsRon?.ToString("N0") ?? "0"} RON");
        }

        if (ctx.Creditors.Any())
        {
            sb.AppendLine("\n## CREDITORS");
            foreach (var cr in ctx.Creditors.Take(20))
                sb.AppendLine($"- {cr}");
            if (ctx.Creditors.Count > 20)
                sb.AppendLine($"... and {ctx.Creditors.Count - 20} more.");
            sb.AppendLine($"Total creditor exposure: {ctx.TotalCreditorValue:N0} RON");
        }

        if (ctx.Assets.Any())
        {
            sb.AppendLine("\n## ASSETS");
            foreach (var a in ctx.Assets.Take(20))
                sb.AppendLine($"- {a}");
        }

        if (ctx.WorkflowStages.Any())
        {
            sb.AppendLine("\n## WORKFLOW STAGES");
            foreach (var s in ctx.WorkflowStages)
                sb.AppendLine($"- {s}");
        }

        if (ctx.OpenTasks.Any())
        {
            sb.AppendLine("\n## OPEN TASKS");
            foreach (var t in ctx.OpenTasks.Take(20))
                sb.AppendLine($"- {t}");
        }

        if (ctx.UpcomingDeadlines.Any())
        {
            sb.AppendLine("\n## UPCOMING DEADLINES");
            foreach (var d in ctx.UpcomingDeadlines)
                sb.AppendLine($"- {d}");
        }

        if (ctx.UpcomingCalendarEvents.Any())
        {
            sb.AppendLine("\n## UPCOMING HEARINGS / MEETINGS");
            foreach (var e in ctx.UpcomingCalendarEvents)
                sb.AppendLine($"- {e}");
        }

        if (ctx.RecentEvents.Any())
        {
            sb.AppendLine("\n## RECENT ACTIVITY (last 30 days)");
            foreach (var e in ctx.RecentEvents.Take(40))
                sb.AppendLine($"- {e}");
        }

        if (ctx.RecentTaskNotes.Any())
        {
            sb.AppendLine("\n## RECENT TASK NOTES");
            foreach (var n in ctx.RecentTaskNotes.Take(15))
                sb.AppendLine($"- {n}");
        }

        sb.AppendLine($"\n---");
        sb.AppendLine($"Please generate the case summary in language: **{language}**");

        return sb.ToString();
    }

    private static string BuildChatSystemPrompt(CaseAiContext ctx, string language)
    {
        var c = ctx.Case;
        var langLabel = language switch { "ro" => "Romanian", "hu" => "Hungarian", _ => "English" };

        var lawContext = language switch
        {
            "ro" => "Răspunde în ROMÂNĂ. Ai cunoștințe solide despre Legea nr. 85/2014 privind procedurile de insolvență din România.",
            "hu" => "Válaszolj MAGYARUL. Jól ismered a romániai fizetésképtelenségi eljárásokat szabályozó 2014. évi 85. törvényt.",
            _ => "Respond in ENGLISH. You have deep knowledge of Romanian insolvency law (Law 85/2014) and its practical application.",
        };

        var sb = new StringBuilder();
        sb.AppendLine($"""
            You are an expert AI assistant for the Insolvex insolvency case management platform.
            You specialise in Romanian insolvency proceedings under Law 85/2014.
            {lawContext}

            CURRENT CASE CONTEXT:
            Case: {c.CaseNumber} | Debtor: {c.DebtorName} (CUI: {c.DebtorCui ?? "N/A"})
            Court: {c.CourtName ?? "N/A"} | Status: {c.Status} | Procedure: {c.ProcedureType}
            Practitioner: {c.PractitionerName ?? "Unassigned"}
            Opening: {c.OpeningDate?.ToString("dd.MM.yyyy") ?? "N/A"} | Claims deadline: {c.ClaimsDeadline?.ToString("dd.MM.yyyy") ?? "N/A"}
            """);

        if (ctx.Creditors.Any())
        {
            sb.AppendLine($"\nCreditors ({ctx.Creditors.Count} total, {ctx.TotalCreditorValue:N0} RON):");
            foreach (var cr in ctx.Creditors.Take(15))
                sb.AppendLine($"  {cr}");
        }

        if (ctx.Assets.Any())
        {
            sb.AppendLine($"\nAssets ({ctx.Assets.Count}):");
            foreach (var a in ctx.Assets.Take(10))
                sb.AppendLine($"  {a}");
        }

        if (ctx.OpenTasks.Any())
        {
            sb.AppendLine($"\nOpen tasks ({ctx.OpenTasks.Count}):");
            foreach (var t in ctx.OpenTasks.Take(10))
                sb.AppendLine($"  {t}");
        }

        if (ctx.RecentEvents.Any())
        {
            sb.AppendLine("\nRecent activity:");
            foreach (var e in ctx.RecentEvents.Take(20))
                sb.AppendLine($"  {e}");
        }

        if (ctx.UpcomingDeadlines.Any())
        {
            sb.AppendLine("\nUpcoming deadlines:");
            foreach (var d in ctx.UpcomingDeadlines)
                sb.AppendLine($"  {d}");
        }

        sb.AppendLine($"""

            IMPORTANT:
            - Always respond in {langLabel}.
            - Be concise and actionable. Use markdown formatting.
            - Reference specific Romanian insolvency law articles when relevant.
            - If you don't have enough information, say so clearly.
            - You can help with: procedure questions, deadline calculations, document drafting guidance, creditor priority, and case strategy.
            """);

        return sb.ToString();
    }

    private static List<object> BuildChatMessages(string systemPrompt, List<AiChatMessage> history, string newMessage)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var h in history)
            messages.Add(new { role = h.Role, content = h.Content });

        messages.Add(new { role = "user", content = newMessage });
        return messages;
    }

    // ── AI provider call (text response, not JSON) ─────────────────────────────

    private async Task<(string? Text, int Tokens)> CallAiAsync(
        AiConfigDto config, string apiKey,
        string systemPrompt, string userPrompt,
        int maxTokens, CancellationToken ct)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt },
        };
        return await CallAiChatAsync(config, apiKey, messages, maxTokens, ct);
    }

    private async Task<(string? Text, int Tokens)> CallAiChatAsync(
        AiConfigDto config, string apiKey,
        List<object> messages, int maxTokens, CancellationToken ct)
    {
        try
        {
            return config.Provider switch
            {
                "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, messages, maxTokens, ct),
                "Anthropic"   => await CallAnthropicAsync(config, apiKey, messages, maxTokens, ct),
                "Google"      => await CallGoogleAsync(config, apiKey, messages, maxTokens, ct),
                _             => await CallOpenAiCompatibleAsync(config, apiKey, messages, maxTokens, ct),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI call failed (provider: {Provider})", config.Provider);
            return (null, 0);
        }
    }

    private async Task<(string? Text, int Tokens)> CallOpenAiCompatibleAsync(
        AiConfigDto config, string apiKey,
        List<object> messages, int maxTokens, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.openai.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "gpt-4o";

        var body = new { model, messages, max_tokens = maxTokens, temperature = 0.3 };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{baseUrl}/v1/chat/completions", content, ct);
        if (!response.IsSuccessStatusCode) return (null, 0);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        var tokens = TryGetTokens(doc.RootElement);
        return (text, tokens);
    }

    private async Task<(string? Text, int Tokens)> CallAzureOpenAiAsync(
        AiConfigDto config, string apiKey,
        List<object> messages, int maxTokens, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01";

        var body = new { messages, max_tokens = maxTokens, temperature = 0.3 };
        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("api-key", apiKey);
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content, ct);
        if (!response.IsSuccessStatusCode) return (null, 0);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        var tokens = TryGetTokens(doc.RootElement);
        return (text, tokens);
    }

    private async Task<(string? Text, int Tokens)> CallAnthropicAsync(
        AiConfigDto config, string apiKey,
        List<object> messages, int maxTokens, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "claude-3-5-sonnet-20241022";

        // Anthropic requires system to be top-level, not in messages
        string? systemContent = null;
        var anthropicMessages = new List<object>();
        foreach (var m in messages)
        {
            var json = JsonSerializer.Serialize(m);
            using var doc = JsonDocument.Parse(json);
            var role = doc.RootElement.GetProperty("role").GetString();
            var msgContent = doc.RootElement.GetProperty("content").GetString() ?? "";
            if (role == "system") { systemContent = msgContent; continue; }
            anthropicMessages.Add(new { role, content = msgContent });
        }

        var body = new { model, max_tokens = maxTokens, system = systemContent, messages = anthropicMessages };
        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var reqContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{baseUrl}/v1/messages", reqContent, ct);
        if (!response.IsSuccessStatusCode) return (null, 0);

        var jsonStr = await response.Content.ReadAsStringAsync(ct);
        using var docR = JsonDocument.Parse(jsonStr);
        var text = docR.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        var tokens = 0;
        if (docR.RootElement.TryGetProperty("usage", out var usage))
        {
            tokens += usage.TryGetProperty("input_tokens", out var inp) ? inp.GetInt32() : 0;
            tokens += usage.TryGetProperty("output_tokens", out var out_) ? out_.GetInt32() : 0;
        }
        return (text, tokens);
    }

    private async Task<(string? Text, int Tokens)> CallGoogleAsync(
        AiConfigDto config, string apiKey,
        List<object> messages, int maxTokens, CancellationToken ct)
    {
        var model = config.ModelName ?? "gemini-1.5-pro";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // Merge system + user into a single content array for Google
        var combinedText = new StringBuilder();
        foreach (var m in messages)
        {
            var json = JsonSerializer.Serialize(m);
            using var doc = JsonDocument.Parse(json);
            var role = doc.RootElement.GetProperty("role").GetString()?.ToUpperInvariant() ?? "USER";
            var content = doc.RootElement.GetProperty("content").GetString() ?? "";
            combinedText.AppendLine($"[{role}]: {content}");
        }

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = combinedText.ToString() } } } },
            generationConfig = new { temperature = 0.3, maxOutputTokens = maxTokens },
        };
        var http = _http.CreateClient();
        var reqContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, reqContent, ct);
        if (!response.IsSuccessStatusCode) return (null, 0);

        var jsonStr = await response.Content.ReadAsStringAsync(ct);
        using var docR = JsonDocument.Parse(jsonStr);
        var text = docR.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        return (text, 0); // Google token usage not easily available
    }

    private static int TryGetTokens(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage)) return 0;
        var total = 0;
        if (usage.TryGetProperty("total_tokens", out var t)) total = t.GetInt32();
        else
        {
            if (usage.TryGetProperty("prompt_tokens", out var p)) total += p.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var c)) total += c.GetInt32();
        }
        return total;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Guid> GetCaseTenantIdAsync(Guid caseId, CancellationToken ct)
        => await _db.InsolvencyCases.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == caseId).Select(c => c.TenantId).FirstOrDefaultAsync(ct);

    private static AiChatMessageDto ToDto(AiChatMessage m, string? userName) => new(
        m.Id, m.Role, m.Content, m.TokensUsed, m.Model, m.CreatedAt, m.UserId, userName);
}
