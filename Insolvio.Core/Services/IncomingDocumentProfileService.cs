using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

/// <summary>
/// Manages the per-tenant incoming document profile database.
///
/// Each profile stores the reference PDF storage key, the visual field annotations
/// drawn in the PDF annotator tool, and AI-generated summaries (EN/RO/HU) and
/// structured parameter descriptions used for automatic document recognition.
///
/// AI analysis is called for all three supported languages in a single service
/// call so the DB row is fully populated in one round-trip.
/// </summary>
public sealed class IncomingDocumentProfileService
{
    private readonly IApplicationDbContext _db;
    private readonly IAiConfigService _aiConfig;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<IncomingDocumentProfileService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IncomingDocumentProfileService(
        IApplicationDbContext db,
        IAiConfigService aiConfig,
        ICurrentUserService currentUser,
        IHttpClientFactory http,
        ILogger<IncomingDocumentProfileService> logger)
    {
        _db = db;
        _aiConfig = aiConfig;
        _currentUser = currentUser;
        _http = http;
        _logger = logger;
    }

    // ── Create on upload (always new row) ─────────────────────────────────────

    /// <summary>
    /// Called when an admin uploads a reference PDF. Always creates a new DB row —
    /// multiple profiles per (TenantId, DocumentType) are supported.
    /// Returns the newly created profile's Id.
    /// </summary>
    public async Task<Guid> CreateOnUploadAsync(
        string documentType,
        string storageKey,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken ct)
    {
        var profile = new IncomingDocumentProfile
        {
            DocumentType = documentType,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            FileSizeBytes = fileSizeBytes,
            UploadedOn = DateTime.UtcNow,
            IsActive = true,
        };
        _db.IncomingDocumentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile.Id;
    }

    /// <summary>
    /// Legacy upsert — kept for backward compatibility with callers that don't
    /// yet track a profile ID. Delegates to CreateOnUploadAsync.
    /// </summary>
    public Task UpsertOnUploadAsync(
        string documentType,
        string storageKey,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken ct)
        => CreateOnUploadAsync(documentType, storageKey, originalFileName, fileSizeBytes, ct)
               .ContinueWith(_ => { }, ct);

    // ── Save annotations ─────────────────────────────────────────────────────

    /// <summary>Persists annotation JSON and optional notes to a specific profile by Id.</summary>
    public async Task SaveAnnotationsByIdAsync(
        Guid profileId,
        string annotationsJson,
        string? notes,
        CancellationToken ct)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new KeyNotFoundException($"Incoming document profile {profileId} not found.");

        if (profile.IsFinalized)
            throw new InvalidOperationException("Cannot modify a finalized training document.");

        profile.AnnotationsJson = annotationsJson;
        profile.AnnotationNotes = notes;
        profile.LastAnnotatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Persists annotation JSON to the most-recent non-finalized profile for a type.
    /// Creates a stub profile if none exists (legacy fallback).
    /// </summary>
    public async Task SaveAnnotationsAsync(
        string documentType,
        string annotationsJson,
        string? notes,
        CancellationToken ct)
    {
        var profile = await FindLatestEditableAsync(documentType, ct);

        if (profile is null)
        {
            profile = new IncomingDocumentProfile
            {
                DocumentType = documentType,
                StorageKey = $"incoming-reference/{documentType}.pdf",
                OriginalFileName = $"{documentType}.pdf",
                FileSizeBytes = 0,
                UploadedOn = DateTime.UtcNow,
                IsActive = true,
            };
            _db.IncomingDocumentProfiles.Add(profile);
        }

        profile.AnnotationsJson = annotationsJson;
        profile.AnnotationNotes = notes;
        profile.LastAnnotatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Read profiles ─────────────────────────────────────────────────────────

    /// <summary>Returns a specific profile by Id, or null if not found.</summary>
    public Task<IncomingDocumentProfile?> GetProfileByIdAsync(Guid profileId, CancellationToken ct)
        => _db.IncomingDocumentProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);

    /// <summary>Returns the most-recent active profile for a document type, or null if none exist.</summary>
    public Task<IncomingDocumentProfile?> GetProfileAsync(string documentType, CancellationToken ct)
        => FindAsync(documentType, ct);

    /// <summary>Returns all profiles for a document type ordered by upload date descending.</summary>
    public Task<List<IncomingDocumentProfile>> GetAllProfilesForTypeAsync(string documentType, CancellationToken ct)
        => _db.IncomingDocumentProfiles
              .Where(p => p.DocumentType == documentType)
              .OrderByDescending(p => p.UploadedOn)
              .ToListAsync(ct);

    // ── Finalize & Train ──────────────────────────────────────────────────────

    /// <summary>
    /// Locks a profile for editing and submits it to the AI training pipeline.
    /// Steps: mark IsFinalized=true, run AI analysis to populate summaries + parameters,
    /// then set TrainingStatus="submitted".
    /// </summary>
    public async Task<IncomingDocumentProfile> FinalizeAndTrainAsync(Guid profileId, CancellationToken ct)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new KeyNotFoundException($"Incoming document profile {profileId} not found.");

        if (profile.IsFinalized)
            return profile; // idempotent

        if (string.IsNullOrWhiteSpace(profile.AnnotationsJson))
            throw new InvalidOperationException("Annotations must be saved before finalizing a training document.");

        // Lock the profile
        profile.IsFinalized = true;
        profile.FinalizedOn = DateTime.UtcNow;
        profile.TrainingStatus = "submitted";
        await _db.SaveChangesAsync(ct);

        // Run AI analysis in the background (non-blocking to the HTTP response)
        // We run it synchronously here so the caller gets the result immediately
        try
        {
            await RunAiAnalysisOnProfileAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed during finalize for profile {ProfileId}", profileId);
        }

        return profile;
    }

    /// <summary>Deletes a non-finalized profile. Finalized profiles cannot be deleted.</summary>
    public async Task DeleteProfileAsync(Guid profileId, CancellationToken ct)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new KeyNotFoundException($"Incoming document profile {profileId} not found.");

        if (profile.IsFinalized)
            throw new InvalidOperationException("Finalized training documents cannot be deleted.");

        _db.IncomingDocumentProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);
    }

    // ── AI analysis ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs AI analysis for a document type (operates on the most-recent active profile).
    /// </summary>
    public async Task<IncomingDocumentProfile?> AnalyseAsync(string documentType, CancellationToken ct)
    {
        var profile = await FindAsync(documentType, ct)
            ?? throw new InvalidOperationException($"No profile found for document type '{documentType}'.");

        await RunAiAnalysisOnProfileAsync(profile, ct);
        return profile;
    }

    /// <summary>Runs AI analysis on a specific profile by Id.</summary>
    public async Task<IncomingDocumentProfile?> AnalyseByIdAsync(Guid profileId, CancellationToken ct)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new InvalidOperationException($"No profile found with Id '{profileId}'.");

        await RunAiAnalysisOnProfileAsync(profile, ct);
        return profile;
    }

    private async Task RunAiAnalysisOnProfileAsync(IncomingDocumentProfile profile, CancellationToken ct)
    {
        try
        {
            var config = await _aiConfig.GetAsync(ct);
            if (!config.IsEnabled)
            {
                _logger.LogInformation("AI is disabled; skipping analysis for profile {Id}", profile.Id);
                return;
            }

            var apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("AI API key empty; skipping analysis for profile {Id}", profile.Id);
                return;
            }

            var annotationsSummary = BuildAnnotationSummary(profile.AnnotationsJson);

            var tasks = new[]
            {
                CallAiForLanguageAsync(config, apiKey, profile.DocumentType, annotationsSummary, profile.AnnotationNotes, "en", ct),
                CallAiForLanguageAsync(config, apiKey, profile.DocumentType, annotationsSummary, profile.AnnotationNotes, "ro", ct),
                CallAiForLanguageAsync(config, apiKey, profile.DocumentType, annotationsSummary, profile.AnnotationNotes, "hu", ct),
            };

            var results = await Task.WhenAll(tasks);

            profile.AiSummaryEn = results[0]?.Summary;
            profile.AiSummaryRo = results[1]?.Summary;
            profile.AiSummaryHu = results[2]?.Summary;

            var bestParams = results.FirstOrDefault(r => !string.IsNullOrEmpty(r?.ParametersJson));
            if (bestParams?.ParametersJson is not null)
                profile.AiParametersJson = bestParams.ParametersJson;

            profile.AiConfidence = results
                .Where(r => r?.Confidence is not null)
                .Select(r => r!.Confidence!.Value)
                .DefaultIfEmpty(0)
                .Average();

            profile.AiModel = $"{config.Provider}/{config.ModelName}";
            profile.AiAnalysedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed for incoming document profile {Id}", profile.Id);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Returns the most-recent active profile for a type (any finalized state).</summary>
    private Task<IncomingDocumentProfile?> FindAsync(string documentType, CancellationToken ct)
        => _db.IncomingDocumentProfiles
              .OrderByDescending(p => p.UploadedOn)
              .FirstOrDefaultAsync(p => p.DocumentType == documentType, ct);

    /// <summary>Returns the most-recent non-finalized profile for a type.</summary>
    private Task<IncomingDocumentProfile?> FindLatestEditableAsync(string documentType, CancellationToken ct)
        => _db.IncomingDocumentProfiles
              .OrderByDescending(p => p.UploadedOn)
              .FirstOrDefaultAsync(p => p.DocumentType == documentType && !p.IsFinalized, ct);

    internal static string BuildAnnotationSummary(string? annotationsJson)
    {
        if (string.IsNullOrWhiteSpace(annotationsJson)) return "(no annotations saved yet)";

        try
        {
            using var doc = JsonDocument.Parse(annotationsJson);
            // Expect the payload shape: { annotations: [...], notes: "..." }
            // OR a raw array. Handle both.
            JsonElement arrEl;
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("annotations", out var inner))
            {
                arrEl = inner;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                arrEl = doc.RootElement;
            }
            else
            {
                return "(could not parse annotations)";
            }

            var lines = new List<string>();
            foreach (var item in arrEl.EnumerateArray())
            {
                var field        = item.TryGetProperty("field",         out var f)  ? f.GetString()  : null;
                var label        = item.TryGetProperty("label",         out var l)  ? l.GetString()  : null;
                var selectedText = item.TryGetProperty("selectedText",  out var st) ? st.GetString() : null;
                var ctxBefore    = item.TryGetProperty("contextBefore", out var cb) ? cb.GetString() : null;
                var ctxAfter     = item.TryGetProperty("contextAfter",  out var ca) ? ca.GetString() : null;

                if (field is not null)
                {
                    if (selectedText is not null)
                        lines.Add($"- {label ?? field}: \"{selectedText}\" (context: …{ctxBefore}[{selectedText}]{ctxAfter}…)");
                    else
                        lines.Add($"- {label ?? field}: (no text captured)");
                }
            }

            return lines.Count == 0
                ? "(annotations list was empty)"
                : string.Join("\n", lines);
        }
        catch
        {
            return "(could not parse annotations)";
        }
    }

    private record AiLanguageResult(string? Summary, string? ParametersJson, double? Confidence);

    private async Task<AiLanguageResult?> CallAiForLanguageAsync(
        Core.DTOs.AiConfigDto config,
        string apiKey,
        string documentType,
        string annotationsSummary,
        string? adminNotes,
        string language,
        CancellationToken ct)
    {
        try
        {
            var (prompt, system) = BuildAnalysisPrompt(documentType, annotationsSummary, adminNotes, language);

            var rawJson = config.Provider switch
            {
                "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, prompt, system, ct),
                "Anthropic"   => await CallAnthropicAsync(config, apiKey, prompt, system, ct),
                "Google"      => await CallGoogleAsync(config, apiKey, prompt, system, ct),
                _             => await CallOpenAiCompatibleAsync(config, apiKey, prompt, system, ct),
            };

            if (rawJson is null) return null;
            return ParseLanguageResult(rawJson);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AI language pass '{Language}' failed for {DocType}", language, documentType);
            return null;
        }
    }

    private static (string Prompt, string System) BuildAnalysisPrompt(
        string documentType, string annotationsSummary, string? adminNotes, string language)
    {
        var (langInstr, langName) = language switch
        {
            "ro" => ("Write the summary and all description fields in Romanian.", "Romanian"),
            "hu" => ("Write the summary and all description fields in Hungarian.", "Hungarian"),
            _    => ("Write the summary and all description fields in English.", "English"),
        };

        var system = $"You are an expert insolvency document recognition specialist. " +
                     $"Analyze incoming document types and generate structured descriptions " +
                     $"that allow automated systems to match and classify documents. " +
                     $"Always respond with valid JSON only. {langInstr}";

        var prompt = $$"""
            Analyze this incoming insolvency document type and generate a structured recognition profile.
            Return ONLY valid JSON — no markdown, no explanation.

            Document type key: {{documentType}}
            {{(adminNotes is not null ? $"Admin notes: {adminNotes}" : "")}}

            Annotated field positions (relative to the PDF page):
            {{annotationsSummary}}

            Respond in {{langName}}.

            Return JSON with this exact structure:
            {
              "summary": "2-4 sentence description in {{langName}} of what this document is, its purpose in insolvency proceedings, and typical structure",
              "fieldParameters": [
                {
                  "field": "FieldKey",
                  "label": "Human-readable field name in {{langName}}",
                  "description": "What this field contains, typical format, expected values",
                  "typical_position": "top-left|top-center|top-right|center|bottom; relative description",
                  "data_format": "e.g. DD/MM/YYYY for dates, RO followed by digits for CUI, free text",
                  "matching_keywords": ["keyword1", "keyword2"]
                }
              ],
              "layoutFeatures": ["feature1 in {{langName}}", "feature2"],
              "matchingRules": ["rule1 in {{langName}}", "rule2"],
              "documentSignatures": ["unique phrase or feature that identifies this document type"],
              "confidence": 0.0-1.0
            }
            """;

        return (prompt, system);
    }

    private static AiLanguageResult? ParseLanguageResult(string rawJson)
    {
        try
        {
            var trimmed = rawJson.Trim();
            if (trimmed.StartsWith("```"))
            {
                var s = trimmed.IndexOf('{');
                var e = trimmed.LastIndexOf('}');
                if (s >= 0 && e > s) trimmed = trimmed[s..(e + 1)];
            }

            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var sv) ? sv.GetString() : null;

            // Build a cleaned parameters JSON (everything except summary)
            var parametersObj = new Dictionary<string, object?>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "summary") continue;
                parametersObj[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            var confidence = root.TryGetProperty("confidence", out var cv) ? cv.GetDouble() : (double?)null;
            var paramsJson = JsonSerializer.Serialize(parametersObj, _jsonOpts);

            return new AiLanguageResult(summary, paramsJson, confidence);
        }
        catch
        {
            return null;
        }
    }

    // ── AI provider implementations (mirrors AiDocumentAnalysisService pattern) ──

    private async Task<string?> CallOpenAiCompatibleAsync(
        Core.DTOs.AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? (config.Provider == "OpenRouter" ? "https://openrouter.ai/api/v1" : "https://api.openai.com")
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "gpt-4o";

        var body = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user",   content = prompt },
            },
            max_tokens = 2000,
            temperature = 0.2,
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var chatUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
        var resp = await http.PostAsync(
            chatUrl,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    private async Task<string?> CallAzureOpenAiAsync(
        Core.DTOs.AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01";

        var body = new
        {
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user",   content = prompt },
            },
            max_tokens = 2000,
            temperature = 0.2,
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("api-key", apiKey);
        var resp = await http.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }

    private async Task<string?> CallAnthropicAsync(
        Core.DTOs.AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "claude-3-5-sonnet-20241022";

        var body = new
        {
            model,
            max_tokens = 2000,
            messages = new[]
            {
                new { role = "user", content = system + "\n\n" + prompt + "\n\nRespond with valid JSON only." },
            },
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var resp = await http.PostAsync(
            $"{baseUrl}/v1/messages",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
    }

    private async Task<string?> CallGoogleAsync(
        Core.DTOs.AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var model = config.ModelName ?? "gemini-1.5-pro";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = system + "\n\n" + prompt + "\n\nRespond with valid JSON only." } } },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2,
                maxOutputTokens = 2000,
            },
        };

        var http = _http.CreateClient();
        var resp = await http.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);

        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }
}
