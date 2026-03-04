using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.Core.Services;

/// <summary>
/// Calls the configured AI provider (OpenAI, Azure OpenAI, Anthropic, Google, Custom)
/// to extract structured insolvency case data from a document's text.
/// Falls back gracefully when AI is disabled or the call fails.
/// </summary>
public sealed class AiDocumentAnalysisService
{
    private const double SpecializedPassThreshold = 0.80;

    private readonly IAiConfigService _aiConfig;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<AiDocumentAnalysisService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AiDocumentAnalysisService(
        IAiConfigService aiConfig,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHttpClientFactory http,
        ILogger<AiDocumentAnalysisService> logger)
    {
        _aiConfig = aiConfig;
        _db = db;
        _currentUser = currentUser;
        _http = http;
        _logger = logger;
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public sealed record AiAnalysisResult(
        string? DocType,
        string? CaseNumber,
        string? DebtorName,
        string? DebtorCui,
        string? CourtName,
        string? CourtSection,
        string? JudgeSyndic,
        string? ProcedureType,
        DateTime? OpeningDate,
        DateTime? NextHearingDate,
        DateTime? ClaimsDeadline,
        DateTime? ContestationsDeadline,
        List<AiExtractedParty> Parties,
        double Confidence);

    public sealed record AiExtractedParty(
        string Role,
        string Name,
        string? FiscalId);

    /// <summary>
    /// Attempt AI-powered extraction. Returns null when AI is unavailable or disabled,
    /// allowing the caller to fall back to heuristic extraction.
    /// </summary>
    /// <param name="extractedText">Raw text extracted from the document.</param>
    /// <param name="fileName">Original file name (hint for AI).</param>
    /// <param name="annotationContext">
    /// Optional: a text summary of field annotations from the document profile
    /// (e.g. "Opening date: top-right corner, ~20% from top").
    /// When provided this is prepended to the AI prompt as structural guidance.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AiAnalysisResult?> AnalyzeAsync(
        string extractedText, string fileName,
        string? annotationContext = null,
        CancellationToken ct = default)
    {
        try
        {
            var config = await _aiConfig.GetAsync(ct);
            if (!config.IsEnabled) return null;

            var apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            var language = await ResolveTenantLanguageAsync(ct);
            var prompt = BuildPrompt(extractedText, fileName, language, annotationContext);
            var systemInstruction = BuildSystemInstruction(language);

            var rawJson = config.Provider switch
            {
                "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, prompt, systemInstruction, ct),
                "Anthropic"   => await CallAnthropicAsync(config, apiKey, prompt, systemInstruction, ct),
                "Google"      => await CallGoogleAsync(config, apiKey, prompt, systemInstruction, ct),
                _             => await CallOpenAiCompatibleAsync(config, apiKey, prompt, systemInstruction, ct), // OpenAI + Custom
            };

            if (rawJson is null) return null;

            var initial = ParseAiResponse(rawJson);
            if (initial is null) return null;

            if (initial.Confidence >= SpecializedPassThreshold)
                return initial;

            var specialized = await RunSpecializedCourtDeadlinePassAsync(
                config,
                apiKey,
                extractedText,
                fileName,
                language,
                initial,
                ct);

            return specialized is null ? initial : MergeSpecialized(initial, specialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI document analysis failed for '{FileName}' — falling back to heuristic extraction", fileName);
            return null;
        }
    }

    private async Task<AiAnalysisResult?> RunSpecializedCourtDeadlinePassAsync(
        AiConfigDto config,
        string apiKey,
        string extractedText,
        string fileName,
        string language,
        AiAnalysisResult baseline,
        CancellationToken ct)
    {
        try
        {
            var prompt = BuildSpecializedPrompt(extractedText, fileName, language, baseline);
            var systemInstruction = BuildSpecializedSystemInstruction(language);
            var specializedModel = GetSpecializedModel(config.Provider, config.ModelName);

            var rawJson = config.Provider switch
            {
                "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, prompt, systemInstruction, ct, specializedModel),
                "Anthropic"   => await CallAnthropicAsync(config, apiKey, prompt, systemInstruction, ct, specializedModel),
                "Google"      => await CallGoogleAsync(config, apiKey, prompt, systemInstruction, ct, specializedModel),
                _              => await CallOpenAiCompatibleAsync(config, apiKey, prompt, systemInstruction, ct, specializedModel),
            };

            if (rawJson is null) return null;
            return ParseAiResponse(rawJson);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Specialized extraction pass failed for '{FileName}'", fileName);
            return null;
        }
    }

    private static AiAnalysisResult MergeSpecialized(AiAnalysisResult baseline, AiAnalysisResult specialized)
    {
        return baseline with
        {
            CourtName = specialized.CourtName ?? baseline.CourtName,
            CourtSection = specialized.CourtSection ?? baseline.CourtSection,
            JudgeSyndic = specialized.JudgeSyndic ?? baseline.JudgeSyndic,
            OpeningDate = specialized.OpeningDate ?? baseline.OpeningDate,
            NextHearingDate = specialized.NextHearingDate ?? baseline.NextHearingDate,
            ClaimsDeadline = specialized.ClaimsDeadline ?? baseline.ClaimsDeadline,
            ContestationsDeadline = specialized.ContestationsDeadline ?? baseline.ContestationsDeadline,
            Confidence = Math.Max(baseline.Confidence, specialized.Confidence),
        };
    }

    // ── Prompt ────────────────────────────────────────────────────────────────

    private static string BuildPrompt(string extractedText, string fileName, string language, string? annotationContext = null)
    {
        // Truncate very long texts to stay within token limits (~4000 chars is enough for structured extraction)
        var textSnippet = extractedText.Length > 6000
            ? extractedText[..6000] + "\n[...document continues...]\n"
            : extractedText;

        var langInstruction = language switch
        {
            "ro" => "Use Romanian legal understanding. Keep JSON keys in English exactly as specified.",
            "hu" => "Use Hungarian legal language understanding where applicable. Keep JSON keys in English exactly as specified.",
            _ => "Use English legal understanding. Keep JSON keys in English exactly as specified.",
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
              "debtorName": "full company name or null",
              "debtorCui": "company fiscal code (CUI/CIF/VAT) or null",
              "courtName": "full court/tribunal name in source language or null",
              "courtSection": "section name or null",
              "judgeSyndic": "judge syndic full name or null",
              "procedureType": "FalimentSimplificat|Faliment|Insolventa|Reorganizare|ConcordatPreventiv|MandatAdHoc|Other",
              "openingDate": "YYYY-MM-DD or null",
              "nextHearingDate": "YYYY-MM-DD or null",
              "claimsDeadline": "YYYY-MM-DD or null",
              "contestationsDeadline": "YYYY-MM-DD or null",
              "parties": [
                {
                  "role": "Debtor|InsolvencyPractitioner|Court|SecuredCreditor|UnsecuredCreditor|BudgetaryCreditor|EmployeeCreditor|JudgeSyndic|CourtExpert|CreditorsCommittee|SpecialAdministrator|Guarantor|ThirdParty",
                  "name": "party full name",
                  "fiscalId": "CUI/CIF e.g. RO12345678 or null"
                }
              ],
              "confidence": 0.0-1.0
            }

            Notes:
            - "deschiderea procedurii" = opening date
            - "termen depunere creante" / "termenul de declarare a creantelor" = claims deadline
            - "termen contestatii" = contestation deadline
            - "urmatorul termen" / "urmatoare sedinta" = next hearing date
            - "lichidator judiciar" / "administrator judiciar" = InsolvencyPractitioner
            - "judecator sindic" = JudgeSyndic (also add as a party with role JudgeSyndic if mentioned by name)
            - Document file name hint: {{fileName}}

            Document text:
            ---
            {{textSnippet}}
            ---
            """;
    }

    private static string BuildSystemInstruction(string language) => language switch
    {
        "ro" => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in Romanian. Keep enum values exactly as requested.",
        "hu" => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in Hungarian and Romanian if present. Keep enum values exactly as requested.",
        _ => "You are an expert insolvency document analysis assistant. Always respond with valid JSON only. Interpret legal terms in English/Romanian. Keep enum values exactly as requested.",
    };

    private static string BuildSpecializedSystemInstruction(string language) => language switch
    {
        "ro" => "You are a specialized Romanian court metadata extractor. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
        "hu" => "You are a specialized court metadata extractor for Hungarian and Romanian legal documents. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
        _ => "You are a specialized court metadata extractor. Prioritize exact tribunal name, section and legal deadlines. Return valid JSON only.",
    };

    private static string BuildSpecializedPrompt(string extractedText, string fileName, string language, AiAnalysisResult baseline)
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

    private static string? GetSpecializedModel(string provider, string? currentModel)
    {
        return provider switch
        {
            "OpenAI" or "Custom" => string.Equals(currentModel, "gpt-4.1", StringComparison.OrdinalIgnoreCase) ? "gpt-4o" : "gpt-4.1",
            "Anthropic" => string.Equals(currentModel, "claude-3-7-sonnet-latest", StringComparison.OrdinalIgnoreCase)
                ? "claude-3-5-sonnet-20241022"
                : "claude-3-7-sonnet-latest",
            "Google" => string.Equals(currentModel, "gemini-2.0-pro-exp-02-05", StringComparison.OrdinalIgnoreCase)
                ? "gemini-1.5-pro"
                : "gemini-2.0-pro-exp-02-05",
            "AzureOpenAI" => currentModel,
            _ => currentModel,
        };
    }

    private async Task<string> ResolveTenantLanguageAsync(CancellationToken ct)
    {
        if (_currentUser.TenantId is null) return "en";

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == _currentUser.TenantId.Value)
            .Select(t => new { t.Language, t.Region })
            .FirstOrDefaultAsync(ct);

        var explicitLanguage = tenant?.Language?.ToLowerInvariant();
        if (explicitLanguage is "ro" or "hu")
            return explicitLanguage;

        // Region-driven default when tenant language is unset or left at default "en"
        if (tenant?.Region == SystemRegion.Romania) return "ro";
        if (tenant?.Region == SystemRegion.Hungary) return "hu";

        return tenant?.Region switch
        {
            SystemRegion.Romania => "ro",
            SystemRegion.Hungary => "hu",
            _ => "en",
        };
    }

    // ── Provider implementations ──────────────────────────────────────────────

    private async Task<string?> CallOpenAiCompatibleAsync(
        AiConfigDto config, string apiKey, string prompt, string systemInstruction, CancellationToken ct, string? modelOverride = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.openai.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = modelOverride ?? config.ModelName ?? "gpt-4o";

        var body = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = prompt },
            },
            max_tokens = 1500,
            temperature = 0.1,
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl}/v1/chat/completions", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("OpenAI API returned {StatusCode}: {Error}", response.StatusCode, err[..Math.Min(500, err.Length)]);
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

    private async Task<string?> CallAzureOpenAiAsync(
        AiConfigDto config, string apiKey, string prompt, string systemInstruction, CancellationToken ct, string? deploymentOverride = null)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = deploymentOverride ?? config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        const string apiVersion = "2024-02-01";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        var body = new
        {
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = prompt },
            },
            max_tokens = 1500,
            temperature = 0.1,
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private async Task<string?> CallAnthropicAsync(
        AiConfigDto config, string apiKey, string prompt, string systemInstruction, CancellationToken ct, string? modelOverride = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = modelOverride ?? config.ModelName ?? "claude-3-5-sonnet-20241022";

        var body = new
        {
            model,
            max_tokens = 1500,
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
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
    }

    private async Task<string?> CallGoogleAsync(
        AiConfigDto config, string apiKey, string prompt, string systemInstruction, CancellationToken ct, string? modelOverride = null)
    {
        var model = modelOverride ?? config.ModelName ?? "gemini-1.5-pro";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = systemInstruction + "\n\n" + prompt + "\n\nRespond with valid JSON only." } } },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1,
                maxOutputTokens = 1500,
            },
        };

        var httpClient = _http.CreateClient();
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    private AiAnalysisResult? ParseAiResponse(string rawJson)
    {
        // Strip markdown code fences if the model wrapped the JSON
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

        var parties = new List<AiExtractedParty>();
        if (root.TryGetProperty("parties", out var partiesEl) && partiesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in partiesEl.EnumerateArray())
            {
                var role = p.TryGetProperty("role", out var r) ? r.GetString() : null;
                var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                var fid  = p.TryGetProperty("fiscalId", out var f) ? f.GetString() : null;
                if (role != null && name != null)
                    parties.Add(new AiExtractedParty(role, name, fid));
            }
        }

        var confidence = root.TryGetProperty("confidence", out var confEl)
            ? confEl.GetDouble()
            : 0.75; // Default moderate confidence for AI-extracted data

        return new AiAnalysisResult(
            DocType:               GetString(root, "docType"),
            CaseNumber:            GetString(root, "caseNumber"),
            DebtorName:            GetString(root, "debtorName"),
            DebtorCui:             GetString(root, "debtorCui"),
            CourtName:             GetString(root, "courtName"),
            CourtSection:          GetString(root, "courtSection"),
            JudgeSyndic:           GetString(root, "judgeSyndic"),
            ProcedureType:         GetString(root, "procedureType"),
            OpeningDate:           ParseDate(root, "openingDate"),
            NextHearingDate:       ParseDate(root, "nextHearingDate"),
            ClaimsDeadline:        ParseDate(root, "claimsDeadline"),
            ContestationsDeadline: ParseDate(root, "contestationsDeadline"),
            Parties:               parties,
            Confidence:            confidence);
    }

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
}
