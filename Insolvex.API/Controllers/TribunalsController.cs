using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TribunalsController : ControllerBase
{
  private readonly ITribunalService _tribunals;

  public TribunalsController(ITribunalService tribunals) => _tribunals = tribunals;

  /// <summary>Get all tribunals visible to the current user.</summary>
  [HttpGet]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetAll(CancellationToken ct)
      => Ok(await _tribunals.GetAllAsync(ct));

  /// <summary>Get a single tribunal by ID.</summary>
  [HttpGet("{id:guid}")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var dto = await _tribunals.GetByIdAsync(id, ct);
    return dto is null ? NotFound() : Ok(dto);
  }

  /// <summary>Create a tribunal record (GlobalAdmin: global; TenantAdmin: tenant override).</summary>
  [HttpPost]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Create([FromBody] TribunalRequest request, CancellationToken ct)
  {
    var dto = await _tribunals.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
  }

  /// <summary>Update a tribunal record.</summary>
  [HttpPut("{id:guid}")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Update(Guid id, [FromBody] TribunalRequest request, CancellationToken ct)
      => Ok(await _tribunals.UpdateAsync(id, request, ct));

  /// <summary>Delete a tribunal record.</summary>
  [HttpDelete("{id:guid}")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    await _tribunals.DeleteAsync(id, ct);
    return NoContent();
  }

  /// <summary>Import tribunal records from a CSV file.</summary>
  [HttpPost("import-csv")]
  [RequirePermission(Permission.SettingsEdit)]
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> ImportCsv([FromForm] IFormFile file, CancellationToken ct)
  {
    if (file is null || file.Length == 0)
      return BadRequest(new { message = "No file uploaded." });

    var result = await _tribunals.ImportCsvAsync(file.OpenReadStream(), ct);
    return Ok(result);
  }

  /// <summary>Export tribunal records to CSV.</summary>
  [HttpGet("export-csv")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> ExportCsv(CancellationToken ct)
  {
    var bytes = await _tribunals.ExportCsvAsync(ct);
    return File(bytes, "text/csv", $"tribunals_{DateTime.UtcNow:yyyyMMdd}.csv");
  }
}