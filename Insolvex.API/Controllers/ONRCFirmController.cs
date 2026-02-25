using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

/// <summary>
/// ONRC (Companies House) firm database: search and import.
/// Data is per system region (Romania, Hungary, etc.).
/// </summary>
[ApiController]
[Route("api/onrc")]
[Authorize]
public class ONRCFirmController : ControllerBase
{
  private readonly IONRCFirmService _onrc;

  public ONRCFirmController(IONRCFirmService onrc) => _onrc = onrc;

  /// <summary>Search the ONRC firm database by CUI or Name.</summary>
  [HttpGet("search")]
  public async Task<IActionResult> Search(
      [FromQuery] string q,
      [FromQuery] SystemRegion region = SystemRegion.Romania,
 [FromQuery] int maxResults = 10,
CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(q))
      return BadRequest(new { message = "Query parameter 'q' is required." });

    var results = await _onrc.SearchAsync(q, region, Math.Min(maxResults, 50), ct);
    return Ok(results);
  }

  /// <summary>Search by CUI specifically.</summary>
  [HttpGet("search/cui")]
  public async Task<IActionResult> SearchByCui(
      [FromQuery] string cui,
      [FromQuery] SystemRegion region = SystemRegion.Romania,
      CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(cui))
      return BadRequest(new { message = "CUI is required." });

    return Ok(await _onrc.SearchByCuiAsync(cui, region, 10, ct));
  }

  /// <summary>Search by company name.</summary>
  [HttpGet("search/name")]
  public async Task<IActionResult> SearchByName(
      [FromQuery] string name,
      [FromQuery] SystemRegion region = SystemRegion.Romania,
CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return BadRequest(new { message = "Name is required." });

    return Ok(await _onrc.SearchByNameAsync(name, region, 10, ct));
  }

  /// <summary>Import firms from a CSV file (up to 700 MB).</summary>
  [HttpPost("import")]
  [RequirePermission(Permission.SettingsEdit)]
  [DisableRequestSizeLimit]
  [RequestFormLimits(MultipartBodyLengthLimit = 700_000_000)] // 700 MB — matches Kestrel and FormOptions
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> ImportCsv(
 [FromForm] IFormFile file,
      [FromQuery] SystemRegion region = SystemRegion.Romania,
      CancellationToken ct = default)
  {
    if (file is null || file.Length == 0)
      return BadRequest(new { message = "No file provided." });

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext != ".csv")
      return BadRequest(new { message = "Only CSV files are accepted." });

    await using var stream = file.OpenReadStream();
    var result = await _onrc.ImportFromCsvAsync(stream, region, ct);
    return Ok(result);
  }

  /// <summary>Get ONRC database statistics for a region.</summary>
  [HttpGet("stats")]
  public async Task<IActionResult> GetStats(
 [FromQuery] SystemRegion region = SystemRegion.Romania,
      CancellationToken ct = default)
  {
    return Ok(await _onrc.GetStatsAsync(region, ct));
  }
}
