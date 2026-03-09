using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.Core.Services;

/// <summary>
/// Unified AI document service.
/// Merges the former AiDocumentAnalysisService (text extraction) and
/// DocumentVisionExtractionService (image extraction) into a single implementation
/// that shares provider HTTP logic and resolves config via the
/// tenant key → global key hierarchy used by CaseAiService.
/// </summary>
public sealed class DocumentAiService : IDocumentAiService
{
    private const double SpecializedPassThreshold = 0.80;
    private const int VisionMaxTokens = 2200;

    private readonly IAiConfigService _aiConfig;
    private readonly ITenantAiConfigService _tenantAiConfig;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<DocumentAiService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DocumentAiService(
        IAiConfigService aiConfig,
        ITenantAiConfigService tenantAiConfig,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHttpClientFactory http,
        ILogger<DocumentAiService> logger)
    {
        _aiConfig = aiConfig;
        _tenantAiConfig = tenantAiConfig;
        _db = db;
        _currentUser = currentUser;
        _http = http;
        _logger = logger;
    }

    // ── Text analysis ─────────────────────────────────────────────────────────

    public async Task<AiDocumentTextResult?> AnalyzeTextAsync(
        string extractedText,
        string fileName,
        string? annotationContext = null,
        CancellationToken ct = default)
    {
        try
        {
            var (config, apiKey) = await ResolveConfigAsync(ct);
            if (config is null || string.IsNullOrWhiteSpace(apiKey)) return null;

            var language = await ResolveTenantLanguageAsync(ct);
            var prompt = BuildTextPrompt(extractedText, fileName, language, annotationContext);
            var systemInstruction = BuildTextSystemInstruction(language);

            var rawJson = await CallTextAsync(config, apiKey, prompt, systemInstruction, ct);
            if (rawJson is null) return null;

            var initial = ParseTextResponse(rawJson);
            if (initial is null) return null;

            if (initial.Confidence >= SpecializedPassThreshold)
                return initial;

            var specialized = await RunSpecializedPassAsync(config, apiKey, extractedText, fileName, language, initial, ct);
            return specialized is null ? initial : MergeSpecialized(initial, specialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI text analysis failed for '{FileName}' — falling back to heuristic extraction", fileName);
            return null;
        }
    }

    // ── Image extraction ──────────────────────────────────────────────────────

    public async Task<JsonElement?> ExtractFromImagesAsync(
        IReadOnlyList<string> base64Images,
        CancellationToken ct = default)
    {
        if (base64Images.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(base64Images));

        try
        {
            var (config, apiKey) = await ResolveConfigAsync(ct);
            if (config is null || string.IsNullOrWhiteSpace(apiKey)) return null;

            string? rawContent = config.Provider switch
            {
                "AzureOpenAI" => await CallAzureVisionAsync(config, apiKey, base64Images, ct),
                "Anthropic"   => await CallAnthropicVisionAsync(config, apiKey, base64Images, ct),
                "Google"      => await CallGoogleVisionAsync(config, apiKey, base64Images, ct),
                _             => await CallOpenAiVisionAsync(config, apiKey, base64Images, ct),
            };

            if (rawContent is null) return null;

            var trimmed = rawContent.Trim();
            if (trimmed.StartsWith("```"))
            {
                var start = trimmed.IndexOf('{');
                var end   = trimmed.LastIndexOf('}');
                if (start >= 0 && end > start)
                    trimmed = trimmed[start..(end + 1)];
            }

            var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI vision extraction returned invalid JSON");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI vision extraction failed");
            return null;
        }
    }

    // ── Config resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolves effective AI config and API key using the tenant→global fallback pattern.
    /// Returns (null, null) when AI is disabled or no key is available.
    /// </summary>
    private async Task<(AiConfigDto? config, string? apiKey)> ResolveConfigAsync(CancellationToken ct)
    {
        var globalConfig = await _aiConfig.GetAsync(ct);

        if (_currentUser.TenantId is Guid tenantId)
        {
            var tenantCfg = await _tenantAiConfig.GetAsync(tenantId, ct);
            if (tenantCfg.AiEnabled)
            {
                var tenantKey = await _tenantAiConfig.GetDecryptedApiKeyAsync(tenantId, ct);
                if (!string.IsNullOrWhiteSpace(tenantKey))
                {
                    var effectiveConfig = globalConfig with
                    {
                        Provider    = tenantCfg.Provider    ?? globalConfig.Provider,
                        ApiEndpoint = tenantCfg.ApiEndpoint ?? globalConfig.ApiEndpoint,
                        ModelName   = tenantCfg.ModelName   ?? globalConfig.ModelName,
                        IsEnabled   = true,
                    };
                    return (effectiveConfig, tenantKey);
                }
            }
        }

        // Fall back to global config
        if (!globalConfig.IsEnabled) return (null, null);
        var globalKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
        return string.IsNullOrWhiteSpace(globalKey) ? (null, null) : (globalConfig, globalKey);
    }

    // ── Text provider dispatch ────────────────────────────────────────────────

    private Task<string?> CallTextAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct,
        string? modelOverride = null) => config.Provider switch
    {
        "AzureOpenAI" => CallAzureTextAsync(config, apiKey, prompt, systemInstruction, ct, modelOverride),
        "Anthropic"   => CallAnthropicTextAsync(config, apiKey, prompt, systemInstruction, ct, modelOverride),
        "Google"      => CallGoogleTextAsync(config, apiKey, prompt, systemInstruction, ct, modelOverride),
        _             => CallOpenAiTextAsync(config, apiKey, prompt, systemInstruction, ct, modelOverride),
    };

    // ── Text provider implementations ─────────────────────────────────────────

    private async Task<string?> CallOpenAiTextAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct, string? modelOverride = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? (config.Provider == "OpenRouter" ? "https://openrouter.ai/api/v1" : "https://api.openai.com")
            : config.ApiEndpoint.TrimEnd('/');
        var model = modelOverride ?? config.ModelName ?? "gpt-4o";

        var body = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user",   content = prompt },
            },
            max_tokens = 2000,
            temperature = 0.1,
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var chatUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
        return await PostAndReadChoiceAsync(httpClient, chatUrl, body, ct);
    }

    private async Task<string?> CallAzureTextAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct, string? deploymentOverride = null)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = deploymentOverride ?? config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01";

        var body = new
        {
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user",   content = prompt },
            },
            max_tokens = 2000,
            temperature = 0.1,
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        return await PostAndReadChoiceAsync(httpClient, url, body, ct);
    }

    private async Task<string?> CallAnthropicTextAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct, string? modelOverride = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = modelOverride ?? config.ModelName ?? "claude-3-5-sonnet-20241022";

        var body = new
        {
            model,
            max_tokens = 2000,
            messages = new[]
            {
                new { role = "user", content = systemInstruction + "\n\n" + prompt + "\n\nRespond with valid JSON only." },
            },
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl}/v1/messages", content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
    }

    private async Task<string?> CallGoogleTextAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct, string? modelOverride = null)
    {
        var model = modelOverride ?? config.ModelName ?? "gemini-1.5-pro";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = systemInstruction + "\n\n" + prompt + "\n\nRespond with valid JSON only." } } },
            },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.1, maxOutputTokens = 2000 },
        };

        var httpClient = _http.CreateClient();
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString();
    }

    // ── Vision provider implementations ───────────────────────────────────────

    private async Task<string?> CallOpenAiVisionAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? (config.Provider == "OpenRouter" ? "https://openrouter.ai/api/v1" : "https://api.openai.com")
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "gpt-4o";

        var body = new
        {
            model,
            response_format = new { type = "json_object" },
            max_tokens = VisionMaxTokens,
            messages = BuildOpenAiVisionMessages(images),
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var chatUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
        return await PostAndReadChoiceAsync(httpClient, chatUrl, body, ct);
    }

    private async Task<string?> CallAzureVisionAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/')
            ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01";

        var body = new
        {
            response_format = new { type = "json_object" },
            max_tokens = VisionMaxTokens,
            messages = BuildOpenAiVisionMessages(images),
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        return await PostAndReadChoiceAsync(httpClient, url, body, ct);
    }

    private async Task<string?> CallAnthropicVisionAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "claude-3-5-sonnet-20241022";

        var contentParts = new List<object>();
        foreach (var img in images)
        {
            var (mediaType, data) = ParseBase64Image(img);
            contentParts.Add(new { type = "image", source = new { type = "base64", media_type = mediaType, data } });
        }
        contentParts.Add(new { type = "text", text = "Analyze the document image(s) above and extract the required fields as JSON. Respond with valid JSON only." });

        var body = new
        {
            model,
            max_tokens = VisionMaxTokens,
            system = ExtractionSystemPrompt,
            messages = new object[] { new { role = "user", content = contentParts } },
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl}/v1/messages", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anthropic vision API returned {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
    }

    private async Task<string?> CallGoogleVisionAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var model = config.ModelName ?? "gemini-1.5-pro";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var parts = new List<object>();
        foreach (var img in images)
        {
            var (mimeType, data) = ParseBase64Image(img);
            parts.Add(new { inlineData = new { mimeType, data } });
        }
        parts.Add(new { text = ExtractionSystemPrompt + "\n\nAnalyze the document image(s) above. Respond with valid JSON only." });

        var body = new
        {
            contents = new object[] { new { parts } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.1, maxOutputTokens = VisionMaxTokens },
        };

        var httpClient = _http.CreateClient();
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0]
            .GetProperty("text").GetString();
    }

    // ── Shared HTTP helper ────────────────────────────────────────────────────

    private async Task<string?> PostAndReadChoiceAsync(HttpClient httpClient, string url, object body, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("AI API at {Url} returned {StatusCode}: {Error}",
                url, response.StatusCode, err[..Math.Min(500, err.Length)]);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    /// <summary>
    /// Like <see cref="PostAndReadChoiceAsync"/> but returns the raw HTTP error body
    /// as the second tuple element instead of swallowing it. Used only by
    /// <see cref="SuggestAnnotationsAsync"/> so the failure reason can be surfaced
    /// to the user without changing any other AI call path.
    /// </summary>
    private async Task<(string? content, string? error)> PostAndReadChoiceDetailedAsync(
        HttpClient httpClient, string url, object body, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("AI API at {Url} returned {StatusCode}: {Error}",
                url, response.StatusCode, err[..Math.Min(500, err.Length)]);
            var shortErr = err.Length > 300 ? err[..300] + "…" : err;
            return (null, $"HTTP {(int)response.StatusCode} {response.StatusCode}: {shortErr}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return (doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString(), null);
    }

    /// <summary>
    /// Dispatches to the correct provider and returns <c>(rawJson, errorMessage)</c>.
    /// Captures the HTTP error body so <see cref="SuggestAnnotationsAsync"/> can
    /// surface the real failure reason.
    /// </summary>
    private async Task<(string? rawJson, string? error)> CallTextDetailedAsync(
        AiConfigDto config, string apiKey,
        string prompt, string systemInstruction,
        CancellationToken ct)
    {
        var httpClient = _http.CreateClient();

        switch (config.Provider)
        {
            case "AzureOpenAI":
            {
                var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
                var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
                var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01";
                var body = new
                {
                    response_format = new { type = "json_object" },
                    messages = new[]
                    {
                        new { role = "system", content = systemInstruction },
                        new { role = "user",   content = prompt },
                    },
                    max_tokens = 2000,
                    temperature = 0.1,
                };
                httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
                return await PostAndReadChoiceDetailedAsync(httpClient, url, body, ct);
            }

            case "Anthropic":
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
                        new { role = "user", content = systemInstruction + "\n\n" + prompt + "\n\nRespond with valid JSON only." },
                    },
                };
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                var reqContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{baseUrl}/v1/messages", reqContent, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Anthropic API returned {StatusCode}: {Error}", response.StatusCode, err[..Math.Min(500, err.Length)]);
                    var shortErr = err.Length > 300 ? err[..300] + "…" : err;
                    return (null, $"HTTP {(int)response.StatusCode} {response.StatusCode}: {shortErr}");
                }
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                return (doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString(), null);
            }

            case "Google":
            {
                var model = config.ModelName ?? "gemini-1.5-pro";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = systemInstruction + "\n\n" + prompt + "\n\nRespond with valid JSON only." } } },
                    },
                    generationConfig = new { responseMimeType = "application/json", temperature = 0.1, maxOutputTokens = 2000 },
                };
                var reqContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, reqContent, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Google AI API returned {StatusCode}: {Error}", response.StatusCode, err[..Math.Min(500, err.Length)]);
                    var shortErr = err.Length > 300 ? err[..300] + "…" : err;
                    return (null, $"HTTP {(int)response.StatusCode} {response.StatusCode}: {shortErr}");
                }
                var json = await response.Content.ReadAsStringAsync(ct);
                using var gDoc = JsonDocument.Parse(json);
                return (gDoc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString(), null);
            }

            default: // OpenAI-compatible (incl. OpenRouter)
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
                        new { role = "system", content = systemInstruction },
                        new { role = "user",   content = prompt },
                    },
                    max_tokens = 2000,
                    temperature = 0.1,
                };
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var chatUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? $"{baseUrl}/chat/completions"
                    : $"{baseUrl}/v1/chat/completions";
                return await PostAndReadChoiceDetailedAsync(httpClient, chatUrl, body, ct);
            }
        }
    }

    // ── Vision helpers ────────────────────────────────────────────────────────

    private static object[] BuildOpenAiVisionMessages(IReadOnlyList<string> images)
    {
        var userContent = new List<object>
        {
            new { type = "text", text = "Analyze this Romanian insolvency document and extract the required fields as JSON." },
        };
        foreach (var img in images)
            userContent.Add(new { type = "image_url", image_url = new { url = img, detail = "high" } });

        return new object[]
        {
            new { role = "system", content = ExtractionSystemPrompt },
            new { role = "user",   content = userContent },
        };
    }

    private static (string mediaType, string data) ParseBase64Image(string image)
    {
        if (image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIdx = image.IndexOf(',');
            if (commaIdx > 0)
            {
                var meta      = image[5..commaIdx];
                var mediaType = meta.Split(';')[0];
                return (mediaType, image[(commaIdx + 1)..]);
            }
        }
        return ("image/jpeg", image);
    }

    // ── Text: specialized second pass ─────────────────────────────────────────

    private async Task<AiDocumentTextResult?> RunSpecializedPassAsync(
        AiConfigDto config, string apiKey,
        string extractedText, string fileName, string language,
        AiDocumentTextResult baseline,
        CancellationToken ct)
    {
        try
        {
            var prompt            = BuildSpecializedPrompt(extractedText, fileName, language, baseline);
            var systemInstruction = BuildSpecializedSystemInstruction(language);
            var specializedModel  = GetSpecializedModel(config.Provider, config.ModelName);

            var rawJson = await CallTextAsync(config, apiKey, prompt, systemInstruction, ct, specializedModel);
            return rawJson is null ? null : ParseTextResponse(rawJson);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Specialized extraction pass failed for '{FileName}'", fileName);
            return null;
        }
    }

    private static AiDocumentTextResult MergeSpecialized(AiDocumentTextResult baseline, AiDocumentTextResult specialized) =>
        baseline with
        {
            CourtName             = specialized.CourtName             ?? baseline.CourtName,
            CourtSection          = specialized.CourtSection          ?? baseline.CourtSection,
            JudgeSyndic           = specialized.JudgeSyndic           ?? baseline.JudgeSyndic,
            Registrar             = specialized.Registrar             ?? baseline.Registrar,
            OpeningDate           = specialized.OpeningDate           ?? baseline.OpeningDate,
            NextHearingDate       = specialized.NextHearingDate       ?? baseline.NextHearingDate,
            ClaimsDeadline        = specialized.ClaimsDeadline        ?? baseline.ClaimsDeadline,
            ContestationsDeadline = specialized.ContestationsDeadline ?? baseline.ContestationsDeadline,
            Confidence            = Math.Max(baseline.Confidence, specialized.Confidence),
        };

    // ── Tenant language resolution ────────────────────────────────────────────

    private async Task<string> ResolveTenantLanguageAsync(CancellationToken ct)
    {
        if (_currentUser.TenantId is null) return "en";

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == _currentUser.TenantId.Value)
            .Select(t => new { t.Language, t.Region })
            .FirstOrDefaultAsync(ct);

        var explicitLanguage = tenant?.Language?.ToLowerInvariant();
        if (explicitLanguage is "ro" or "hu") return explicitLanguage;

        return tenant?.Region switch
        {
            SystemRegion.Romania => "ro",
            SystemRegion.Hungary => "hu",
            _                   => "en",
        };
    }

    // ── Text prompt builders ──────────────────────────────────────────────────

    private static string BuildTextSystemInstruction(string language) => language switch
    {
        "ro" => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in Romanian. Keep enum values exactly as requested.",
        "hu" => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in Hungarian and Romanian if present. Keep enum values exactly as requested.",
        _    => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in English/Romanian. Keep enum values exactly as requested.",
    };

    private static string BuildSpecializedSystemInstruction(string language) => language switch
    {
        "ro" => "You are a specialized Romanian court metadata extractor. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
        "hu" => "You are a specialized court metadata extractor for Hungarian and Romanian legal documents. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
        _    => "You are a specialized court metadata extractor. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
    };

    private static string BuildTextPrompt(string extractedText, string fileName, string language, string? annotationContext = null)
    {
        var textSnippet = extractedText.Length > 6000
            ? extractedText[..6000] + "\n[...document continues...]\n"
            : extractedText;

        var langInstruction = language switch
        {
            "ro" => "Use Romanian legal understanding. Keep JSON keys in English exactly as specified.",
            "hu" => "Use Hungarian legal language understanding where applicable. Keep JSON keys in English exactly as specified.",
            _    => "Use English legal understanding. Keep JSON keys in English exactly as specified.",
        };

        var annotationSection = !string.IsNullOrWhiteSpace(annotationContext)
            ? $"""

            Field annotation hints (where specific fields are located in the document layout):
            {annotationContext}

            Use these location hints to prioritise which text regions contain each field.
            """
            : string.Empty;

        return $$"""
            Analyze the insolvency document below and extract structured data.
            Return ONLY a valid JSON object — no markdown, no explanation.
            {{langInstruction}}{{annotationSection}}

            Required JSON structure:
            {
              "docType": "court_decision|report|petition|claims_table|notification|contract|invoice|bpi_publication|unknown",
              "caseNumber": "tribunal/number/year format e.g. \"123/1234/2023\" or null",
              "debtorName": "Full legal company name of the debtor as it appears in the document. May include legal-form suffix such as SRL, S.R.L., SA, S.A., SNC, SCS, RA, REGIE AUTONOMA, SRL-D, PFA, II, IF. Return the full name including suffix. Return null if not found.",
              "debtorCui": "Romanian company tax ID (CUI/CIF). Look for labels: CIF, CUI, Cod Unic de Inregistrare, Cod de Identificare Fiscala, or 'RO' followed by digits. Return ONLY the digits (strip the 'RO' prefix). Typically 2-10 digits. Do NOT return trade registry numbers (J../../../..), EUID, CNP, or foreign VAT codes. If both CIF and CUI appear, use the numeric value. If uncertain, return null.",
              "courtName": "full court/tribunal name in source language or null",
              "courtSection": "The full section phrase as printed in the header, e.g. 'Secția a II-a Civilă'. STOP at the first newline — the section name is a single line. Do NOT append anything that follows on a new line (e.g. 'Dosar nr', case numbers, dates). Return null if not present.",
              "judgeSyndic": "The judge-syndic's personal name ONLY, taken from the SAME LINE as the label 'Judecător sindic' / 'JUDECĂTOR SINDIC'. STOP at the end of that line (first newline). Strip the job-title prefix and any trailing punctuation or whitespace. Collect ALL remaining words on that line — Romanian names frequently have 3 parts (e.g. 'POP MARIA ELENA' or 'ZSIGMOND GABRIELLA ILEANA'). Return ALL words as a single string. Do NOT include the registrar's name or any subsequent line. Return null if not found.",
              "registrar": "The registrar's personal name ONLY, taken from the SAME LINE as the label 'Grefier' / 'GREFIER' / 'Grefier-Șef'. Rules: (1) Strip the label 'Grefier' (and any variant) plus any colon, space or dash that follows it. (2) Read the remaining text on that SAME LINE only — STOP immediately at the first newline, comma, or dash separator (e.g. ' - '). (3) Collect the next 3–4 consecutive ALL_CAPS or Title_Case words; these form the full name (e.g. 'TODOR LOREDANA VASILICA'). (4) Do NOT include any text that appears after a comma or dash separator even if it is on the same line. Return null if not found.",
              "procedureType": "FalimentSimplificat|Faliment|Insolventa|Reorganizare|ConcordatPreventiv|MandatAdHoc|Other",
              "openingDate": "YYYY-MM-DD or null",
              "nextHearingDate": "YYYY-MM-DD or null",
              "claimsDeadline": "YYYY-MM-DD or null",
              "contestationsDeadline": "YYYY-MM-DD or null",
              "parties": [
                {
                  "role": "Debtor|Court|SecuredCreditor|UnsecuredCreditor|BudgetaryCreditor|EmployeeCreditor|JudgeSyndic|CourtExpert|CreditorsCommittee|SpecialAdministrator|Guarantor|ThirdParty",
                  "name": "Full legal name of the party. For companies include the legal-form suffix (SRL, SA, SPRL, etc.). For persons include the full name (1–4 words).",
                  "fiscalId": "CUI/CIF numeric digits only, or null"
                }
              ],
              "confidence": 0.0-1.0
            }

            Notes:
            - "deschiderea procedurii" = opening date
            - "termen depunere creante" / "termenul de declarare a creantelor" = claims deadline
            - "termen contestatii" = contestation deadline
            - "urmatorul termen" / "urmatoare sedinta" = next hearing date
            - "lichidator judiciar" / "administrator judiciar" = the system user (practitioner); do NOT create a party for this role
            - "judecător sindic" / "JUDECĂTOR SINDIC" → read to the END OF THAT LINE only. Collect ALL name words that follow the label on the SAME LINE — Romanian names routinely have 3 parts. STOP at the first newline.
            - "grefier" / "GREFIER" / "Grefier-Şef" → strip label; read remaining text on same line; STOP at newline, comma, or dash separator. Collect 3–4 ALL_CAPS or Title_Case words.
            - For parties, include ALL mentioned parties including fiscal IDs when mentioned.
            - CRITICAL: judgeSyndic, registrar and courtSection are each SINGLE-LINE values.
            - For debtorCui: scan for labels CIF, CUI, "Cod Unic de Inregistrare", "Cod de Identificare Fiscala". Return ONLY the numeric digits — always strip any "RO" prefix. Typically 2-10 digits. Never confuse it with trade registry numbers (e.g. J40/1234/2020), EUID, CNP (13 digits), or other countries' VAT.
            - Document file name hint: {{fileName}}

            Document text:
            ---
            {{textSnippet}}
            ---
            """;
    }

    private static string BuildSpecializedPrompt(string extractedText, string fileName, string language, AiDocumentTextResult baseline)
    {
        var textSnippet = extractedText.Length > 9000
            ? extractedText[..9000] + "\n[...document continues...]"
            : extractedText;

        return $$"""
            Re-analyze ONLY high-risk fields for this insolvency document.
            Use precise legal cues from the source text. Return ONLY JSON.

            Focus fields:
            - courtName
            - courtSection
            - judgeSyndic
            - openingDate
            - nextHearingDate
            - claimsDeadline
            - contestationsDeadline
            - confidence

            Keep all other fields as null if uncertain.

            Existing baseline extraction (may contain errors):
            {
              "courtName": "{{baseline.CourtName}}",
              "courtSection": "{{baseline.CourtSection}}",
              "judgeSyndic": "{{baseline.JudgeSyndic}}",
              "openingDate": "{{baseline.OpeningDate:yyyy-MM-dd}}",
              "nextHearingDate": "{{baseline.NextHearingDate:yyyy-MM-dd}}",
              "claimsDeadline": "{{baseline.ClaimsDeadline:yyyy-MM-dd}}",
              "contestationsDeadline": "{{baseline.ContestationsDeadline:yyyy-MM-dd}}",
              "confidence": {{baseline.Confidence}}
            }

            Required JSON keys (same schema):
            {
              "docType": null,
              "caseNumber": null,
              "debtorName": null,
              "debtorCui": null,
              "courtName": "string or null",
              "courtSection": "string or null",
              "judgeSyndic": "string or null",
              "procedureType": null,
              "openingDate": "YYYY-MM-DD or null",
              "nextHearingDate": "YYYY-MM-DD or null",
              "claimsDeadline": "YYYY-MM-DD or null",
              "contestationsDeadline": "YYYY-MM-DD or null",
              "parties": [],
              "confidence": 0.0-1.0
            }

            Hints:
            - tribunal/court often appears in page header/top-right
            - courtSection: SINGLE LINE value — stop at the first newline.
            - judgeSyndic: SINGLE LINE value — strip the job-title prefix only; keep every remaining word. Romanian names have 2–3 parts.
            - registrar: SINGLE LINE value — strip the 'Grefier' label. STOP at first newline, comma, or ' - ' separator.
            - claims deadline keywords: "termen depunere creante", "termenul de declarare a creantelor"
            - contestations deadline keywords: "termen contestatii", "contestare"
            - preserve source language wording for courtName/section

            Document file name: {{fileName}}
            Language hint: {{language}}

            Document text:
            ---
            {{textSnippet}}
            ---
            """;
    }

    private static string? GetSpecializedModel(string provider, string? currentModel) => provider switch
    {
        "OpenAI" or "Custom" => string.Equals(currentModel, "gpt-4.1", StringComparison.OrdinalIgnoreCase) ? "gpt-4o" : "gpt-4.1",
        "Anthropic" => string.Equals(currentModel, "claude-3-7-sonnet-latest", StringComparison.OrdinalIgnoreCase)
            ? "claude-3-5-sonnet-20241022"
            : "claude-3-7-sonnet-latest",
        "Google" => string.Equals(currentModel, "gemini-2.0-pro-exp-02-05", StringComparison.OrdinalIgnoreCase)
            ? "gemini-1.5-pro"
            : "gemini-2.0-pro-exp-02-05",
        _ => currentModel,
    };

    // ── Annotation suggestion prompt builders ────────────────────────────────

    private static string BuildAnnotationSuggestSystemInstruction(string language) => language switch
    {
        "ro" => "Ești un expert în adnotarea documentelor de insolvență românești. Răspunde EXCLUSIV cu JSON valid. Fiecare valoare returnată trebuie să fie un subșir verbatim exact din textul documentului.",
        "hu" => "Ön egy romániai fizetésképtelenségi dokumentumok szakértő annotátora. Kizárólag érvényes JSON-t adjon vissza. Minden visszaadott értéknek szó szerint szerepelnie kell a dokumentum szövegében.",
        _    => "You are an expert insolvency document annotator. Always respond with valid JSON only. Every value you return must be an exact verbatim substring of the document text.",
    };

    private static string BuildAnnotationSuggestPrompt(string textSnippet, string language)
    {
        var (langInstruction, dateHint, caseHint, procedureHint, courtHint, sectionHint,
             judgeHint, registrarHint, claimsHint, contestHint, hearingHint, openingHint) = language switch
        {
            "ro" => (
                "Documentul este în limba română. Identifică câmpurile folosind terminologia juridică românească.",
                "Returnează datele EXACT cum apar tipărite (ex. \"15.01.2023\" sau \"15 ianuarie 2023\")",
                "numărul dosarului exact cum apare (ex. \"1234/56/2023\")",
                "tipul procedurii de insolvență exact cum apare (ex. \"faliment simplificat\", \"insolvență\")",
                "denumirea completă a tribunalului/instanței exact cum apare",
                "linia secției instanței exact cum apare (ex. \"Secția a II-a Civilă\")",
                "numele complet al judecătorului-sindic exact cum apare (2-3 cuvinte după eticheta 'Judecător sindic')",
                "numele complet al grefierului exact cum apare (2-3 cuvinte după eticheta 'Grefier')",
                "termenul de depunere a creanțelor exact cum apare (caută: 'termen depunere creanțe', 'termen de declarare')",
                "termenul de contestații exact cum apare (caută: 'termen contestații', 'contestare')",
                "data următorului termen exact cum apare (caută: 'termen', 'ședință', 'judecată')",
                "data deschiderii procedurii exact cum apare (caută: 'deschiderea procedurii', 'sentința')"
            ),
            "hu" => (
                "A dokumentum román vagy magyar nyelven van. Azonosítsd a mezőket a jogi terminológia alapján.",
                "Az értékeket pontosan úgy add vissza, ahogy a dokumentumban szerepelnek (pl. \"15.01.2023\" vagy \"15 ianuarie 2023\")",
                "az ügyszám pontosan, ahogy szerepel (pl. \"1234/56/2023\")",
                "a fizetésképtelenségi eljárás típusa pontosan, ahogy szerepel",
                "a bíróság teljes neve pontosan, ahogy szerepel",
                "a bírósági szakasz sora pontosan, ahogy szerepel",
                "a szindikusbíró teljes neve pontosan, ahogy szerepel",
                "a bírósági tisztviselő teljes neve pontosan, ahogy szerepel",
                "a követelések benyújtásának határideje pontosan, ahogy szerepel",
                "a kifogások határideje pontosan, ahogy szerepel",
                "a következő tárgyalás dátuma pontosan, ahogy szerepel",
                "az eljárás megnyitásának dátuma pontosan, ahogy szerepel"
            ),
            _ => (
                "The document may contain Romanian or other language legal text. Identify fields using insolvency legal terminology.",
                "Return dates exactly as printed (e.g. \"15.01.2023\" or \"15 ianuarie 2023\")",
                "the dossier/case number exactly as printed (e.g. \"1234/56/2023\")",
                "the insolvency procedure type phrase as printed",
                "the full tribunal/court name as printed",
                "the court section line as printed",
                "the judge-syndic full name as printed",
                "the registrar full name as printed",
                "the claims filing deadline exactly as printed",
                "the contestations deadline exactly as printed",
                "the next hearing date exactly as printed",
                "the opening date exactly as printed"
            ),
        };

        return $$"""
            {{langInstruction}}
            For each field listed, find the EXACT verbatim text as it appears in the document below.

            Rules:
            - Return ONLY text that EXISTS VERBATIM as a substring of the document
            - Do NOT normalize, translate, paraphrase, or reformat values
            - {{dateHint}}
            - For the procedure type, return the exact phrase as written in the document
            - Each value must be short — the exact identifying phrase, not an entire sentence
            - If a field is not present, return null

            Return ONLY a JSON object with these exact keys:
            {
              "CaseNumber": "{{caseHint}}",
              "ProcedureType": "{{procedureHint}}",
              "OpeningDecisionNo": "the opening decision number/reference as printed",
              "DebtorName": "the full legal name of the debtor company as printed (include SRL/SA suffix)",
              "DebtorCui": "the CUI/CIF fiscal code exactly as printed (digits only, strip 'RO' prefix)",
              "DebtorAddress": "the debtor registered address as printed",
              "CourtName": "{{courtHint}}",
              "CourtSection": "{{sectionHint}}",
              "JudgeSyndic": "{{judgeHint}}",
              "Registrar": "{{registrarHint}}",
              "OpeningDate": "{{openingHint}}",
              "ClaimsDeadline": "{{claimsHint}}",
              "ContestationsDeadline": "{{contestHint}}",
              "NextHearingDate": "{{hearingHint}}"
            }

            Document text:
            ---
            {{textSnippet}}
            ---
            """;
    }

    // ── Text response parsing ─────────────────────────────────────────────────

    private AiDocumentTextResult? ParseTextResponse(string rawJson)
    {
        var trimmed = rawJson.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('{');
            var end   = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                trimmed = trimmed[start..(end + 1)];
        }

        using var doc = JsonDocument.Parse(trimmed);
        var root = doc.RootElement;

        var parties = new List<AiExtractedPartyResult>();
        if (root.TryGetProperty("parties", out var partiesEl) && partiesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in partiesEl.EnumerateArray())
            {
                var role = p.TryGetProperty("role", out var r) ? r.GetString() : null;
                var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                var fid  = p.TryGetProperty("fiscalId", out var f) ? f.GetString() : null;
                if (role != null && name != null)
                    parties.Add(new AiExtractedPartyResult(role, name, fid));
            }
        }

        var confidence = root.TryGetProperty("confidence", out var confEl) ? confEl.GetDouble() : 0.75;

        return new AiDocumentTextResult(
            DocType:               GetString(root, "docType"),
            CaseNumber:            GetString(root, "caseNumber"),
            DebtorName:            GetString(root, "debtorName"),
            DebtorCui:             GetString(root, "debtorCui"),
            CourtName:             GetString(root, "courtName"),
            CourtSection:          CleanCourtSection(GetString(root, "courtSection")),
            JudgeSyndic:           CleanPersonName(GetString(root, "judgeSyndic"), _judgeSyndicTitles),
            Registrar:             CleanPersonName(GetString(root, "registrar"), _registrarTitles),
            ProcedureType:         GetString(root, "procedureType"),
            OpeningDate:           ParseDate(root, "openingDate"),
            NextHearingDate:       ParseDate(root, "nextHearingDate"),
            ClaimsDeadline:        ParseDate(root, "claimsDeadline"),
            ContestationsDeadline: ParseDate(root, "contestationsDeadline"),
            Parties:               parties,
            Confidence:            confidence);
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private static string? GetString(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Null) return null;
        var s = v.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static DateTime? ParseDate(JsonElement el, string key)
    {
        var s = GetString(el, key);
        if (s is null) return null;
        if (DateTime.TryParse(s, out var dt)) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }

    // ── Name / section cleaning ───────────────────────────────────────────────

    private static readonly string[] _judgeSyndicTitles =
        ["JUDECĂTOR-SINDIC", "JUDECATOR-SINDIC", "Judecător-sindic", "Judecator-sindic",
         "JUDECĂTOR SINDIC", "JUDECATOR SINDIC", "Judecător sindic", "Judecator sindic",
         "Judge syndic", "Judge"];

    private static readonly string[] _registrarTitles =
        ["GREFIER-ȘEF", "GREFIER-SEF", "Grefier-Șef", "Grefier-Sef",
         "GREFIER", "Grefier", "Clerk"];

    private static readonly HashSet<string> _personNameStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "GREFIER", "GREFIER-ȘEF", "GREFIER-SEF",
        "JUDECĂTOR", "JUDECATOR", "JUDECĂTORUL", "JUDECATORUL",
        "SINDIC", "SINDIC:", "JUDECĂTOR-SINDIC", "JUDECATOR-SINDIC",
        "DOSAR", "NR", "NR.", "TRIBUNALUL", "JUDECĂTORIA", "JUDECATORIA",
        "CURTEA", "SECTIA", "SECȚIA", "S.C.", "SRL", "S.R.L.", "SA", "S.A.",
    };

    private static readonly Regex _uppercaseHyphenLowercasePattern =
        new(@"([A-ZĂÂÎȘȚŞŢ]+)-[a-zăâîșţţ]", RegexOptions.Compiled);

    private static readonly Regex _upperCaseNameToken =
        new(@"^[A-ZĂÂÎȘȚŞŢ]{2,}(?:-[A-ZĂÂÎȘȚŞŢ]{2,})*$" +
            @"|^[A-ZĂÂÎȘȚŞŢ][a-zăâîșțţ]{1,}(?:-[A-ZĂÂÎȘȚŞŢ][a-zăâîșțţ]{1,})*$", RegexOptions.Compiled);

    private static readonly Regex _courtSectionStopPattern =
        new(@"(Dosar|DOSAR|dosar|Nr\.|nr\.|NR\.?|\s\d{2,}|\d{1,}\/)", RegexOptions.Compiled);

    private static string? CleanCourtSection(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();
        var m = _courtSectionStopPattern.Match(text);
        if (m.Success) text = text[..m.Index].Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? CleanPersonName(string? raw, string[] titlesToStrip)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

        foreach (var title in titlesToStrip.OrderByDescending(t => t.Length))
        {
            if (text.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            {
                text = text[title.Length..].TrimStart(' ', ':', '-').Trim();
                break;
            }
        }

        var commaSep = text.IndexOf(',');
        if (commaSep >= 0) text = text[..commaSep].Trim();

        var dashSep = text.IndexOf(" - ", StringComparison.Ordinal);
        if (dashSep >= 0) text = text[..dashSep].Trim();

        text = _uppercaseHyphenLowercasePattern.Replace(text, m =>
        {
            var run = m.Groups[1].Value;
            return run + " " + m.Value[run.Length..];
        });

        var tokens   = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nameWords = new List<string>(4);

        foreach (var tok in tokens)
        {
            var word = tok.TrimEnd(':', ',', '.', ';', '-');
            if (string.IsNullOrEmpty(word)) break;
            if (_personNameStopWords.Contains(word)) break;
            if (char.IsLower(word[0])) break;
            if (!char.IsLetter(word[0])) break;
            if (!_upperCaseNameToken.IsMatch(word)) break;
            nameWords.Add(word);
            if (nameWords.Count == 4) break;
        }

        return nameWords.Count > 0 ? string.Join(" ", nameWords) : null;
    }

    // ── Image extraction system prompt ────────────────────────────────────────

    /// <summary>
    /// The canonical extraction prompt for Romanian insolvency documents.
    /// Previously embedded in netlify/functions/extract.ts; now owned by the backend.
    /// </summary>
    private const string ExtractionSystemPrompt = """
        You are an expert Romanian insolvency (Legea 85/2014) document analyst for the product "Insolvio".

        You will be shown images of ONE insolvency-related document (court decision, notification,
        claims table, creditors meeting minutes, report art. 97, final report art. 167, etc.) for a
        Romanian case.

        Your job:
        1) Identify the document type.
        2) Extract structured data into the EXACT JSON schema defined below.
        3) Be precise: do not hallucinate. If unsure, use "Not found" or null.
        4) Prefer the most explicit value. If multiple candidates exist, pick the most explicit and
           add alternatives in notes.

        Hard rules:
        - Return ONLY a valid JSON object with EXACTLY these top-level keys:
          document, case, parties, deadlines, claims, creditorsMeeting, reports, complianceFlags, otherImportantInfo
        - Do NOT add extra top-level keys.
        - For any string field that cannot be determined: use "Not found".
        - For any number field that cannot be determined: use null.
        - For any boolean field that cannot be determined: use null.
        - Dates:
          - If you can confidently convert to ISO YYYY-MM-DD, do so in the *iso* fields.
          - If not, keep original date text in *text* fields and set iso to null.
        - Amounts:
          - Extract numeric values as numbers (RON) where possible (e.g., "45.255 lei" -> 45255).
          - If currency is not RON or unclear, still parse number but note currency in notes.
        - Percentages: use numeric 0..100 (e.g., "5%" -> 5).

        DOCUMENT TYPES (use one of these for document.docType):
        - "court_opening_decision"
        - "notification_opening"
        - "report_art_97"
        - "claims_table_preliminary"
        - "claims_table_definitive"
        - "creditors_meeting_minutes"
        - "final_report_art_167"
        - "other"

        PROCEDURE TYPES (for case.procedure.procedureType):
        - "faliment_simplificat"
        - "faliment"
        - "insolventa"
        - "reorganizare"
        - "other"

        PROCEDURE STAGES (for case.procedure.stage):
        - "request" | "opened" | "claims_window" | "preliminary_table" | "definitive_table"
        - "liquidation" | "final_report" | "closure_requested" | "closed" | "unknown"

        DEADLINE TYPES (for deadlines[].type):
        - "claims_submission" | "claims_verification_preliminary_table" | "definitive_table"
        - "creditors_meeting" | "appeal" | "opposition" | "next_hearing" | "other"

        CREDITOR TYPE (for parties.creditors[].creditorType and claims.entries[].creditorType):
        - "bugetar" | "salarial" | "garantat" | "chirografar" | "altul" | "unknown"

        CANONICAL KEY NAMES (use these exactly so the review UI can read fields):
        - document: use "docType" (not "type"), "documentDate" as {text, iso} object (not "issuanceDate").
        - case: use "caseNumber" (not "fileNumber"); use "court" as an object with
          "name", "section", "registryAddress", "registryPhone", "registryHours".
          - For court.section: find the keyword "Secția" or "Sectia", skip any leading ordinal
            (e.g. "a II-a", "I", "II"), then record ONLY the descriptive name that follows.
            Examples: "Secția civilă" → section = "civilă"; "Secția a II-a Civilă" → section = "Civilă".
        - parties: use "debtor" as an object with
          "name", "cui", "tradeRegisterNo", "address", "locality", "county",
          "administrator", "associateOrShareholder", "caen", "incorporationYear", "shareCapitalRon".
        - parties: use "practitioner" (not "appointedLiquidator") with
          "role", "name", "fiscalId", "rfo", "representative", "address",
          "email", "phone", "fax", "appointedDate", "confirmedDate".

        CUI/CIF EXTRACTION RULES (for parties.debtor.cui):
        - Look for labels: "CIF", "CUI", "Cod Unic de Înregistrare", "Cod de Identificare Fiscală",
          or "RO" followed by digits.
        - The value is 2-10 digits, optionally prefixed with "RO". If "RO" is present in the source,
          keep it (e.g. "RO12345678"); otherwise return only the digits.
        - Do NOT confuse with: Nr. Reg. Com. (e.g. J40/1234/2020), EUID, CNP (13 digits starting
          with 1-9), or foreign VAT codes.
        - If no CUI/CIF is found, set "cui" to "Not found".

        JUDGE SYNDIC EXTRACTION RULES (for case.judgeSyndic):
        - Look for the label "judecător sindic", "judecator sindic", or "JUDECĂTOR SINDIC"
          (case-insensitive, with or without diacritics).
        - The value is the full name that follows on the same line, after any colon or whitespace.
        - If not found, set "judgeSyndic" to "Not found".

        REGISTRAR EXTRACTION RULES (for case.registrar):
        - Look for the label "grefier" or "GREFIER" (case-insensitive).
        - The value is the full name on the same line, after any colon or whitespace.
        - If not found, set "registrar" to "Not found".

        Output EXACTLY this JSON schema (string "Not found" for unknown string fields, null for
        numbers/booleans; dates as {text, iso} objects):
        {
          "document": {
            "docType": "court_opening_decision|notification_opening|report_art_97|claims_table_preliminary|claims_table_definitive|creditors_meeting_minutes|final_report_art_167|other",
            "language": "ro",
            "issuingEntity": "Not found",
            "documentNumber": "Not found",
            "documentDate": { "text": "Not found", "iso": null },
            "sourceHints": "Not found"
          },
          "case": {
            "caseNumber": "Not found",
            "court": {
              "name": "Not found",
              "section": "Not found",
              "registryAddress": "Not found",
              "registryPhone": "Not found",
              "registryHours": "Not found"
            },
            "judgeSyndic": "Not found",
            "registrar": "Not found",
            "procedure": {
              "law": "Legea 85/2014",
              "procedureType": "other",
              "stage": "unknown",
              "administrationRightLifted": null,
              "legalBasisArticles": []
            },
            "importantDates": {
              "requestFiledDate": { "text": "Not found", "iso": null },
              "openingDate": { "text": "Not found", "iso": null },
              "nextHearingDateTime": { "text": "Not found", "iso": null }
            }
          },
          "parties": {
            "debtor": {
              "name": "Not found",
              "cui": "Not found",
              "tradeRegisterNo": "Not found",
              "address": "Not found",
              "locality": "Not found",
              "county": "Not found",
              "administrator": "Not found",
              "associateOrShareholder": "Not found",
              "caen": "Not found",
              "incorporationYear": "Not found",
              "shareCapitalRon": null
            },
            "practitioner": {
              "role": "Not found",
              "name": "Not found",
              "fiscalId": "Not found",
              "rfo": "Not found",
              "representative": "Not found",
              "address": "Not found",
              "email": "Not found",
              "phone": "Not found",
              "fax": "Not found",
              "appointedDate": { "text": "Not found", "iso": null },
              "confirmedDate": { "text": "Not found", "iso": null }
            },
            "creditors": []
          },
          "deadlines": [],
          "claims": {
            "tableType": "unknown",
            "tableDate": { "text": "Not found", "iso": null },
            "totalAdmittedRon": null,
            "totalDeclaredRon": null,
            "currency": "RON",
            "entries": []
          },
          "creditorsMeeting": {
            "meetingDate": { "text": "Not found", "iso": null },
            "meetingTime": "Not found",
            "location": "Not found",
            "quorumPercent": null,
            "agenda": [],
            "decisions": {
              "practitionerConfirmed": null,
              "committeeFormed": null,
              "committeeNotes": "Not found",
              "feeApproved": {
                "fixedFeeRon": null,
                "vatIncluded": null,
                "successFeePercent": null,
                "paymentSource": "unknown"
              }
            },
            "votingSummary": "Not found"
          },
          "reports": {
            "art97": {
              "issuedDate": { "text": "Not found", "iso": null },
              "causesOfInsolvency": [],
              "litigationFound": null,
              "avoidanceReview": {
                "reviewed": null,
                "suspiciousTransactionsFound": null,
                "actionsFiled": null,
                "notes": "Not found"
              },
              "liabilityAssessmentArt169": {
                "reviewed": null,
                "culpablePersonsIdentified": null,
                "actionProposedOrFiled": null,
                "notes": "Not found"
              },
              "financials": {
                "yearsCovered": [],
                "totalAssetsRon": null,
                "totalLiabilitiesRon": null,
                "netEquityRon": null,
                "cashRon": null,
                "receivablesRon": null,
                "notes": "Not found"
              }
            },
            "finalArt167": {
              "issuedDate": { "text": "Not found", "iso": null },
              "assetsIdentified": null,
              "saleableAssetsFound": null,
              "sumsAvailableForDistributionRon": null,
              "recoveryRatePercent": null,
              "finalBalanceSheetDate": { "text": "Not found", "iso": null },
              "closureProposed": null,
              "closureLegalBasis": "Not found",
              "deregistrationORCProposed": null,
              "practitionerFeeRequestedFromUNPIR": null,
              "notes": "Not found"
            }
          },
          "complianceFlags": {
            "administrationRightLifted": null,
            "individualActionsSuspended": null,
            "publicationInBPIReferenced": null
          },
          "otherImportantInfo": "Not found"
        }
        """;

    // ── Annotation suggestion ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AnnotationSuggestResult?> SuggestAnnotationsAsync(
        string extractedText,
        CancellationToken ct = default)
    {
        try
        {
            var (config, apiKey) = await ResolveConfigAsync(ct);
            if (config is null || string.IsNullOrWhiteSpace(apiKey)) return null;

            // Language resolution failure must never abort the whole suggestion.
            string language;
            try   { language = await ResolveTenantLanguageAsync(ct); }
            catch { language = "en"; }

            // For long documents use a first-chunk + last-chunk strategy so that
            // both the header (case number, debtor, court) and the dispositiv section
            // (dates, deadlines) are always included.
            const int totalCap = 20_000;
            const int tailLen  =  5_000;
            string textSnippet;
            if (extractedText.Length <= totalCap)
            {
                textSnippet = extractedText;
            }
            else
            {
                textSnippet = extractedText[..(totalCap - tailLen)]
                    + "\n\n[...middle section omitted...]\n\n"
                    + extractedText[^tailLen..];
            }

            var prompt = BuildAnnotationSuggestPrompt(textSnippet, language);
            var system = BuildAnnotationSuggestSystemInstruction(language);

            // Use the detailed variant so the actual HTTP error body is captured.
            var (rawJson, callError) = await CallTextDetailedAsync(config, apiKey, prompt, system, ct);
            if (rawJson is null)
            {
                _logger.LogWarning("Annotation suggestion call failed. Provider error: {Error}", callError);
                return new AnnotationSuggestResult([], CallFailed: true, ErrorMessage: callError);
            }

            var trimmed = rawJson.Trim();
            if (trimmed.StartsWith("```"))
            {
                var start = trimmed.IndexOf('{');
                var end   = trimmed.LastIndexOf('}');
                if (start >= 0 && end > start) trimmed = trimmed[start..(end + 1)];
            }

            using var doc = JsonDocument.Parse(trimmed);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(val) && val.Length >= 2)
                        result[prop.Name] = val;
                }
            }
            return new AnnotationSuggestResult(result, CallFailed: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Annotation suggestion AI call failed");
            return new AnnotationSuggestResult([], CallFailed: true, ErrorMessage: ex.Message);
        }
    }
}
