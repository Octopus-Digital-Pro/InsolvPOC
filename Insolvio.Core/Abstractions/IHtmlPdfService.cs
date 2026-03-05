using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Contract for rendering HTML templates to PDF documents.
/// Implemented by <c>HtmlPdfService</c> in <c>Insolvio.Integrations</c>.
/// </summary>
public interface IHtmlPdfService
{
    /// <summary>
    /// Render the HTML template at <paramref name="templatePath"/> to a PDF,
    /// replacing every <c>{{PlaceholderName}}</c> token with the matching value
    /// from <paramref name="mergeData"/>. The resulting PDF is uploaded to
    /// storage under <paramref name="outputStorageKey"/>.
    /// </summary>
    Task<RenderedDocumentResult> RenderHtmlToPdfAsync(
        string templatePath,
        Dictionary<string, string> mergeData,
        string outputStorageKey,
        CancellationToken ct = default);

    /// <summary>
    /// Convert a pre-rendered HTML string directly to PDF bytes without writing
    /// to storage.
    /// </summary>
    Task<byte[]> RenderHtmlStringToPdfBytesAsync(
        string renderedHtml,
        CancellationToken ct = default);
}
