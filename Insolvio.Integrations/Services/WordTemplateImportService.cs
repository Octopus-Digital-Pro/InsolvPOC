using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;

namespace Insolvio.Integrations.Services;

/// <summary>
/// Imports a .docx Word document and converts it to an HTML Handlebars template.
/// Step 1: DOCX → HTML (DocumentFormat.OpenXml).
/// Step 2: AI analysis — replaces literal data values with {{PlaceholderKey}} tokens
///         using the configured AI provider (OpenAI, Azure OpenAI, Anthropic, Google, Custom).
/// Falls back to raw HTML when AI is unavailable or fails.
/// </summary>
public sealed class WordTemplateImportService
{
    private readonly IAiConfigService _aiConfig;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WordTemplateImportService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public WordTemplateImportService(
        IAiConfigService aiConfig,
        IHttpClientFactory http,
        ILogger<WordTemplateImportService> logger)
    {
        _aiConfig = aiConfig;
        _http = http;
        _logger = logger;
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public sealed record PlaceholderGroupInfo(string Group, IReadOnlyList<PlaceholderFieldInfo> Fields);
    public sealed record PlaceholderFieldInfo(string Key, string Label);
    public sealed record ImportResult(string Html, IReadOnlyList<string> DetectedPlaceholders);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Convert a .docx stream to an HTML template with Handlebars placeholders.
    /// If AI is unavailable or fails, returns the raw HTML without placeholders.
    /// Throws <see cref="InvalidOperationException"/> if the stream is not a valid .docx.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        Stream docxStream,
        IEnumerable<PlaceholderGroupInfo> placeholderGroups,
        CancellationToken ct = default)
    {
        // Step 1: DOCX → HTML
        string rawHtml;
        try
        {
            rawHtml = ExtractHtmlFromDocx(docxStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract HTML from .docx stream");
            throw new InvalidOperationException(
                "The uploaded file could not be read as a Word document (.docx). " +
                "Make sure the file was saved in the modern .docx format (not .doc).", ex);
        }

        if (string.IsNullOrWhiteSpace(rawHtml))
            return new ImportResult("<p></p>", Array.Empty<string>());

        // Step 2: AI placeholder substitution
        try
        {
            var config = await _aiConfig.GetAsync(ct);
            if (config.IsEnabled)
            {
                var apiKey = await _aiConfig.GetDecryptedApiKeyAsync(ct);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var result = await CallAiAsync(config, apiKey, rawHtml, placeholderGroups, ct);
                    if (result is not null) return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI placeholder detection failed during Word import — returning raw HTML");
        }

        return new ImportResult(rawHtml, Array.Empty<string>());
    }

    // ── DOCX → HTML ───────────────────────────────────────────────────────────

    private static string ExtractHtmlFromDocx(Stream stream)
    {
        var sb = new StringBuilder();

        using var wdoc = WordprocessingDocument.Open(stream, false);
        var body = wdoc.MainDocumentPart?.Document?.Body;
        if (body is null) return "";

        var numbering = wdoc.MainDocumentPart?.NumberingDefinitionsPart?.Numbering;

        foreach (var element in body.ChildElements)
        {
            try
            {
                switch (element)
                {
                    case Paragraph para:
                        sb.Append(ConvertParagraph(para, numbering));
                        break;
                    case Table table:
                        sb.Append(ConvertTable(table));
                        break;
                }
            }
            catch
            {
                // Be tolerant with vendor-specific/unknown OOXML nodes or enum tokens.
                // Fall back to escaped inner text for this element instead of failing the whole import.
                var text = element.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append("<p>" + System.Net.WebUtility.HtmlEncode(text) + "</p>\n");
            }
        }

        return sb.ToString();
    }

    private static string ConvertParagraph(Paragraph para, Numbering? _numbering)
    {
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.ToLowerInvariant() ?? "";

        var tag = styleId switch
        {
            "heading1" or "heading 1" or "titlu1" or "titlu 1" or "1" => "h1",
            "heading2" or "heading 2" or "titlu2" or "titlu 2" or "2" => "h2",
            "heading3" or "heading 3" or "titlu3" or "titlu 3" or "3" => "h3",
            _ => "p"
        };

        var jc = SafeJustificationValue(para.ParagraphProperties?.Justification?.Val);
        string alignStyle;
        if (jc == JustificationValues.Center)       alignStyle = " style=\"text-align:center\"";
        else if (jc == JustificationValues.Right)  alignStyle = " style=\"text-align:right\"";
        else if (jc == JustificationValues.Both)   alignStyle = " style=\"text-align:justify\"";
        else                                       alignStyle = "";

        var innerHtml = ConvertRuns(para);
        if (string.IsNullOrWhiteSpace(innerHtml))
            return "<br/>\n";

        return $"<{tag}{alignStyle}>{innerHtml}</{tag}>\n";
    }

    private static string ConvertRuns(Paragraph para)
    {
        var sb = new StringBuilder();

        foreach (var run in para.Elements<Run>())
        {
            var texts = run.Elements<Text>().Select(t => t.Text);
            var text = string.Concat(texts);
            if (string.IsNullOrEmpty(text)) continue;

            // Escape HTML entities
            text = System.Net.WebUtility.HtmlEncode(text);

            var rpr = run.RunProperties;

            // Apply formatting (innermost first so nesting is correct)
            var ulVal = SafeUnderlineValue(rpr?.Underline?.Val);
            if (ulVal == UnderlineValues.Single || ulVal == UnderlineValues.Words)
                text = $"<u>{text}</u>";
            if (rpr?.Italic is not null)
                text = $"<em>{text}</em>";
            if (rpr?.Bold is not null)
                text = $"<strong>{text}</strong>";

            sb.Append(text);
        }

        return sb.ToString();
    }

    private static string ConvertTable(Table table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"width:100%; border-collapse:collapse;\">");

        bool firstRow = true;
        foreach (var row in table.Elements<TableRow>())
        {
            sb.AppendLine("  <tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellTag = firstRow ? "th" : "td";
                var cellContent = string.Concat(
                    cell.Elements<Paragraph>().Select(p => ConvertRuns(p)));
                sb.AppendLine($"    <{cellTag}>{cellContent}</{cellTag}>");
            }
            sb.AppendLine("  </tr>");
            firstRow = false;
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static JustificationValues? SafeJustificationValue(EnumValue<JustificationValues>? value)
    {
        if (value is null) return null;
        try { return value.Value; }
        catch { return null; }
    }

    private static UnderlineValues? SafeUnderlineValue(EnumValue<UnderlineValues>? value)
    {
        if (value is null) return null;
        try { return value.Value; }
        catch { return null; }
    }

    // ── AI integration ────────────────────────────────────────────────────────

    private async Task<ImportResult?> CallAiAsync(
        AiConfigDto config,
        string apiKey,
        string rawHtml,
        IEnumerable<PlaceholderGroupInfo> placeholderGroups,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(rawHtml, placeholderGroups);
        var system = BuildSystemInstruction();

        var rawJson = config.Provider switch
        {
            "AzureOpenAI" => await CallAzureOpenAiAsync(config, apiKey, prompt, system, ct),
            "Anthropic"   => await CallAnthropicAsync(config, apiKey, prompt, system, ct),
            "Google"      => await CallGoogleAsync(config, apiKey, prompt, system, ct),
            _             => await CallOpenAiCompatibleAsync(config, apiKey, prompt, system, ct),
        };

        return rawJson is null ? null : ParseResponse(rawJson);
    }

    private static string BuildSystemInstruction() =>
        "You are an expert document template assistant for an insolvency case management system. " +
        "You convert HTML documents to Handlebars templates by replacing literal data values with {{PlaceholderKey}} tokens. " +
        "Always return a valid JSON object exactly matching the specified structure.";

    private static string BuildPrompt(string rawHtml, IEnumerable<PlaceholderGroupInfo> placeholderGroups)
    {
        // Truncate very large documents to stay within token budgets
        var htmlSnippet = rawHtml.Length > 10_000
            ? rawHtml[..10_000] + "\n<!-- document truncated for analysis -->"
            : rawHtml;

        var placeholderLines = placeholderGroups
            .SelectMany(g => g.Fields.Select(f => "  {{" + f.Key + "}} \u2014 " + f.Label + " (group: " + g.Group + ")"));
        var placeholderList = string.Join("\n", placeholderLines);

        return
            "Convert the HTML document below into a Handlebars template for an insolvency management system.\n\n" +
            "INSTRUCTIONS:\n" +
            "1. Identify specific literal data values in the document (company names, CUI codes, addresses, dates, numbers, court names, people names, etc.)\n" +
            "2. Replace those values with the matching {{Key}} placeholder from the AVAILABLE PLACEHOLDERS list\n" +
            "3. Keep ALL other text, structure, and HTML formatting exactly as-is — do not modify tags, attributes, or non-data text\n" +
            "4. Do NOT insert placeholders where there is no matching field — leave text unchanged\n" +
            "5. For tables that contain repeated rows (e.g. creditor lists, claims tables), wrap the <tr> body row(s) with {{#each CollectionName}} ... {{/each}} and use the field keys from that collection group\n" +
            "6. Return ONLY a JSON object — NO markdown, NO code fences, NO explanation\n\n" +
            "AVAILABLE PLACEHOLDERS:\n" +
            placeholderList + "\n\n" +
            "REQUIRED JSON RESPONSE (no markdown, no extra text):\n" +
            "{\n" +
            "  \"html\": \"<complete processed HTML with {{Key}} placeholders substituted>\",\n" +
            "  \"detectedPlaceholders\": [\"Key1\", \"Key2\"]\n" +
            "}\n\n" +
            "DOCUMENT HTML:\n" +
            "---\n" +
            htmlSnippet + "\n" +
            "---";
    }

    private static ImportResult? ParseResponse(string rawJson)
    {
        // Strip any accidental markdown fences
        var json = rawJson.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('\n') + 1;
            var end = json.LastIndexOf("```");
            if (end > start) json = json[start..end].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("html", out var htmlProp)) return null;
            var html = htmlProp.GetString();
            if (string.IsNullOrWhiteSpace(html)) return null;

            var detected = new List<string>();
            if (root.TryGetProperty("detectedPlaceholders", out var dpProp)
                && dpProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dpProp.EnumerateArray())
                {
                    var s = item.GetString();
                    if (s is not null) detected.Add(s);
                }
            }

            return new ImportResult(html, detected);
        }
        catch
        {
            return null;
        }
    }

    // ── Provider implementations ──────────────────────────────────────────────

    private async Task<string?> CallOpenAiCompatibleAsync(
        AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "https://api.openai.com"
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
            max_tokens = 4096,
            temperature = 0.1,
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.Timeout = TimeSpan.FromSeconds(120);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{baseUrl}/v1/chat/completions", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI returned {Status} during Word template import", response.StatusCode);
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
        AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') 
            ?? throw new InvalidOperationException("Azure OpenAI requires ApiEndpoint.");
        var deployment = config.DeploymentName ?? config.ModelName ?? "gpt-4o";
        const string apiVersion = "2024-02-01";
        var url = $"{baseUrl}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        var body = new
        {
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user",   content = prompt },
            },
            max_tokens = 4096,
            temperature = 0.1,
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("api-key", apiKey);
        http.Timeout = TimeSpan.FromSeconds(120);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content, ct);
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
        AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? "https://api.anthropic.com";
        var model = config.ModelName ?? "claude-3-5-sonnet-20241022";

        var body = new
        {
            model,
            max_tokens = 4096,
            system,
            messages = new[] { new { role = "user", content = prompt } },
        };

        var http = _http.CreateClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        http.Timeout = TimeSpan.FromSeconds(120);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{baseUrl}/v1/messages", content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
    }

    private async Task<string?> CallGoogleAsync(
        AiConfigDto config, string apiKey, string prompt, string system, CancellationToken ct)
    {
        var baseUrl = config.ApiEndpoint?.TrimEnd('/') ?? "https://generativelanguage.googleapis.com";
        var model = config.ModelName ?? "gemini-1.5-pro";
        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"{system}\n\n{prompt}" } } },
            },
            generationConfig = new { responseMimeType = "application/json" },
        };

        var http = _http.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(120);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content, ct);
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
}
