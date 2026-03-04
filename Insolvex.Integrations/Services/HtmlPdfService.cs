using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Insolvex.Core.Abstractions;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Insolvex.Integrations.Services;

/// <summary>
/// Renders an HTML template with {{Placeholder}} token replacement to a PDF
/// using PuppeteerSharp (headless Chromium).
///
/// Chromium binary is downloaded once on first use and cached under the
/// PuppeteerSharp default path (AppData or /root/.cache/puppeteer).
///
/// Usage:
///   - Inject <see cref="HtmlPdfService"/> via DI.
///   - Call <see cref="RenderHtmlToPdfAsync"/> with the full template path,
///     a merge-data dictionary and a storage key for the output file.
/// </summary>
public class HtmlPdfService : IHtmlPdfService
{
    private static readonly SemaphoreSlim _browserInitLock = new(1, 1);
    private static IBrowser? _browser;

    private readonly IFileStorageService _storage;
    private readonly ILogger<HtmlPdfService> _logger;

    public HtmlPdfService(IFileStorageService storage, ILogger<HtmlPdfService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Render <paramref name="templatePath"/> to a PDF, replacing every
    /// <c>{{PlaceholderName}}</c> token with the matching value from
    /// <paramref name="mergeData"/>. The resulting PDF bytes are uploaded to
    /// storage under <paramref name="outputStorageKey"/>.
    /// </summary>
    public async Task<RenderedDocumentResult> RenderHtmlToPdfAsync(
        string templatePath,
        Dictionary<string, string> mergeData,
        string outputStorageKey,
        CancellationToken ct = default)
    {
        if (!File.Exists(templatePath))
            return new RenderedDocumentResult { Error = $"HTML template not found: {templatePath}" };

        try
        {
            // 1. Read template and perform placeholder substitution
            var rawHtml = await File.ReadAllTextAsync(templatePath, Encoding.UTF8, ct);
            var mergedHtml = ApplyMergeData(rawHtml, mergeData);

            // 2. Ensure Chromium is available
            await EnsureChromiumAsync(ct);

            // 3. Render to PDF
            var pdfBytes = await RenderAsync(mergedHtml, ct);

            // 4. Upload to storage
            await using var ms = new MemoryStream(pdfBytes);
            await _storage.UploadAsync(outputStorageKey, ms, "application/pdf", ct);

            // 5. Build result
            var hash = ComputeSha256(pdfBytes);
            var mergeDataJson = JsonSerializer.Serialize(mergeData,
                new JsonSerializerOptions { WriteIndented = false });

            _logger.LogInformation(
                "HtmlPdfService: rendered {Bytes} bytes → {Key}", pdfBytes.Length, outputStorageKey);

            return new RenderedDocumentResult
            {
                Success = true,
                StorageKey = outputStorageKey,
                ContentType = "application/pdf",
                FileSizeBytes = pdfBytes.Length,
                FileHash = hash,
                MergeDataJson = mergeDataJson,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HtmlPdfService: failed to render {Template}", templatePath);
            return new RenderedDocumentResult { Error = ex.Message };
        }
    }
    /// <summary>
    /// Convert a pre-rendered HTML string directly to PDF bytes without writing to storage.
    /// Use this when the HTML was already merged by <see cref="TemplateGenerationService"/>.
    /// </summary>
    public async Task<byte[]> RenderHtmlStringToPdfBytesAsync(
        string renderedHtml,
        CancellationToken ct = default)
    {
        await EnsureChromiumAsync(ct);
        return await RenderAsync(renderedHtml, ct);
    }
    // ── Chromium bootstrap ─────────────────────────────────────────────────

    /// <summary>
    /// Download Chromium on first call (≈ 250 MB); subsequent calls reuse the
    /// cached binary.  Uses a semaphore so parallel requests don't double-download.
    /// </summary>
    private static async Task EnsureChromiumAsync(CancellationToken ct)
    {
        if (_browser is { IsClosed: false })
            return;

        await _browserInitLock.WaitAsync(ct);
        try
        {
            if (_browser is { IsClosed: false })
                return;

            var fetcher = new BrowserFetcher();
            await fetcher.DownloadAsync();

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                },
            });
        }
        finally
        {
            _browserInitLock.Release();
        }
    }

    // ── Rendering ──────────────────────────────────────────────────────────

    private static async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        var browser = _browser ?? throw new InvalidOperationException("Chromium not initialised.");
        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
            Timeout = 30_000,
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "2.5cm",
                Bottom = "2.5cm",
                Left  = "2.5cm",
                Right = "2cm",
            },
        });

        return pdfBytes;
    }

    // ── Token replacement ──────────────────────────────────────────────────

    /// <summary>
    /// Replace every <c>{{Key}}</c> token in <paramref name="html"/> with the
    /// HTML-encoded value from <paramref name="mergeData"/>.
    /// Unknown tokens are left as empty strings.
    /// </summary>
    private static string ApplyMergeData(string html, Dictionary<string, string> mergeData)
    {
        return Regex.Replace(html, @"\{\{(\w+)\}\}", m =>
        {
            var key = m.Groups[1].Value;
            var value = mergeData.TryGetValue(key, out var v) ? v : string.Empty;
            // HTML-encode to prevent injection (labels / addresses may contain &, <, etc.)
            return WebUtility.HtmlEncode(value);
        });
    }

    // ── Utilities ──────────────────────────────────────────────────────────

    private static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
