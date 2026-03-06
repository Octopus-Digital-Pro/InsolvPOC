using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;

namespace Insolvio.Core.Services;

/// <summary>
/// Sends document page images to the tenant/global-configured AI provider and returns
/// the rich structured extraction JSON consumed by the DocumentReviewPage.
///
/// This service owns the extraction prompt and all provider-specific vision call logic
/// (previously living in the Netlify serverless function netlify/functions/extract.ts).
/// It uses whatever AI provider is configured via IAiConfigService — OpenAI, Azure OpenAI,
/// Anthropic, Google, or a custom OpenAI-compatible endpoint.
/// </summary>
public sealed class DocumentVisionExtractionService
{
    private const int MaxTokens = 2200;

    private readonly IAiConfigService _aiConfig;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<DocumentVisionExtractionService> _logger;

    public DocumentVisionExtractionService(
        IAiConfigService aiConfig,
        IHttpClientFactory http,
        ILogger<DocumentVisionExtractionService> logger)
    {
        _aiConfig = aiConfig;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Submits base64-encoded document page images to the configured AI provider and
    /// returns the extraction result as a <see cref="JsonElement"/>.
    /// Returns <c>null</c> when AI is disabled, unconfigured, or the provider call fails.
    /// </summary>
    /// <param name="base64Images">
    /// One or more base64 data URLs (data:image/jpeg;base64,…) or raw base64 strings.
    /// </param>
    public async Task<JsonElement?> ExtractFromImagesAsync(
        IReadOnlyList<string> base64Images,
        CancellationToken ct = default)
    {
        if (base64Images.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(base64Images));

        var config = await _aiConfig.GetAsync(ct);
        if (!config.IsEnabled) return null;

        var apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        string? rawContent = config.Provider switch
        {
            "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, base64Images, ct),
            "Anthropic"   => await CallAnthropicAsync(config, apiKey, base64Images, ct),
            "Google"      => await CallGoogleAsync(config, apiKey, base64Images, ct),
            _             => await CallOpenAiCompatibleAsync(config, apiKey, base64Images, ct),
        };

        if (rawContent is null) return null;

        // Strip markdown code fences if the model wrapped the JSON
        var trimmed = rawContent.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('{');
            var end   = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                trimmed = trimmed[start..(end + 1)];
        }

        try
        {
            var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI vision extraction returned invalid JSON");
            return null;
        }
    }

    // ── OpenAI / Custom ───────────────────────────────────────────────────────

    private async Task<string?> CallOpenAiCompatibleAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.openai.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "gpt-4o";

        var body = new
        {
            model,
            response_format = new { type = "json_object" },
            max_tokens = MaxTokens,
            messages = BuildOpenAiMessages(images),
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        return await PostAndReadChoiceAsync(
            httpClient, $"{baseUrl}/v1/chat/completions", body, ct);
    }

    // ── Azure OpenAI ──────────────────────────────────────────────────────────

    private async Task<string?> CallAzureOpenAiAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/')
            ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        const string apiVersion = "2024-02-01";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        var body = new
        {
            response_format = new { type = "json_object" },
            max_tokens = MaxTokens,
            messages = BuildOpenAiMessages(images),
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        return await PostAndReadChoiceAsync(httpClient, url, body, ct);
    }

    // ── Anthropic ─────────────────────────────────────────────────────────────

    private async Task<string?> CallAnthropicAsync(
        AiConfigDto config, string apiKey, IReadOnlyList<string> images, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.anthropic.com"
            : config.ApiEndpoint.TrimEnd('/');
        var model = config.ModelName ?? "claude-3-5-sonnet-20241022";

        // Anthropic vision: images then user text in a single user message;
        // system instruction passed as the top-level "system" field.
        var contentParts = new List<object>();
        foreach (var img in images)
        {
            var (mediaType, data) = ParseBase64Image(img);
            contentParts.Add(new
            {
                type = "image",
                source = new { type = "base64", media_type = mediaType, data },
            });
        }
        contentParts.Add(new
        {
            type = "text",
            text = "Analyze the document image(s) above and extract the required fields as JSON. Respond with valid JSON only.",
        });

        var body = new
        {
            model,
            max_tokens = MaxTokens,
            system = ExtractionSystemPrompt,
            messages = new object[]
            {
                new { role = "user", content = contentParts },
            },
        };

        var httpClient = _http.CreateClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl}/v1/messages", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Anthropic vision API returned {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
    }

    // ── Google Gemini ─────────────────────────────────────────────────────────

    private async Task<string?> CallGoogleAsync(
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
        parts.Add(new
        {
            text = ExtractionSystemPrompt
                + "\n\nAnalyze the document image(s) above. Respond with valid JSON only.",
        });

        var body = new
        {
            contents = new object[]
            {
                new { parts },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1,
                maxOutputTokens = MaxTokens,
            },
        };

        var httpClient = _http.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds the messages array for OpenAI-compatible (and Azure) chat completions.</summary>
    private static object[] BuildOpenAiMessages(IReadOnlyList<string> images)
    {
        var userContent = new List<object>
        {
            new { type = "text", text = "Analyze this Romanian insolvency document and extract the required fields as JSON." },
        };
        foreach (var img in images)
        {
            userContent.Add(new
            {
                type = "image_url",
                image_url = new { url = img, detail = "high" },
            });
        }

        return new object[]
        {
            new { role = "system", content = ExtractionSystemPrompt },
            new { role = "user",   content = userContent },
        };
    }

    private async Task<string?> PostAndReadChoiceAsync(
        HttpClient httpClient, string url, object body, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Vision extraction API at {Url} returned {StatusCode}: {Error}",
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
    /// Splits a data URL (data:image/jpeg;base64,XXX) into (mediaType, rawBase64).
    /// Falls back to ("image/jpeg", original) when no data URL prefix is present.
    /// </summary>
    private static (string mediaType, string data) ParseBase64Image(string image)
    {
        if (image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIdx = image.IndexOf(',');
            if (commaIdx > 0)
            {
                var meta      = image[5..commaIdx]; // e.g. "image/jpeg;base64"
                var mediaType = meta.Split(';')[0];
                return (mediaType, image[(commaIdx + 1)..]);
            }
        }
        return ("image/jpeg", image);
    }

    // ── Extraction system prompt ──────────────────────────────────────────────

    /// <summary>
    /// The canonical extraction prompt for Romanian insolvency documents.
    /// This prompt was previously embedded in the Netlify serverless function
    /// (netlify/functions/extract.ts SYSTEM_PROMPT) and is now owned by the backend
    /// so it can evolve alongside the rest of the domain logic.
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
}
