using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/local-governments")]
[Authorize]
public class LocalGovernmentsController : ControllerBase
{
  private readonly ILocalGovernmentService _service;

  public LocalGovernmentsController(ILocalGovernmentService service) => _service = service;

  [HttpGet]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetAll(CancellationToken ct)
      => Ok(await _service.GetAllAsync(ct));

  [HttpGet("{id:guid}")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var dto = await _service.GetByIdAsync(id, ct);
    return dto is null ? NotFound() : Ok(dto);
  }

  [HttpPost]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Create([FromBody] LocalGovernmentRequest request, CancellationToken ct)
  {
    var dto = await _service.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
  }

  [HttpPut("{id:guid}")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Update(Guid id, [FromBody] LocalGovernmentRequest request, CancellationToken ct)
      => Ok(await _service.UpdateAsync(id, request, ct));

  [HttpDelete("{id:guid}")]
  [RequirePermission(Permission.SettingsEdit)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    await _service.DeleteAsync(id, ct);
    return NoContent();
  }

  [HttpPost("import-csv")]
  [RequirePermission(Permission.SettingsEdit)]
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> ImportCsv([FromForm] IFormFile file, CancellationToken ct)
  {
    if (file is null || file.Length == 0)
      return BadRequest(new { message = "No file uploaded." });

    var result = await _service.ImportCsvAsync(file.OpenReadStream(), ct);
    return Ok(result);
  }

  [HttpGet("export-csv")]
  [RequirePermission(Permission.SettingsView)]
  public async Task<IActionResult> ExportCsv(CancellationToken ct)
  {
    var bytes = await _service.ExportCsvAsync(ct);
    return File(bytes, "text/csv", $"local_governments_{DateTime.UtcNow:yyyyMMdd}.csv");
  }
}

