using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.DocumentView)]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _docs;

    public DocumentsController(IDocumentService docs) => _docs = docs;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _docs.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequirePermission(Permission.DocumentUpload)]
    public async Task<IActionResult> Create([FromBody] CreateDocBody body, CancellationToken ct)
    {
        var dto = await _docs.CreateAsync(new CreateDocumentCommand
        {
            CaseId = body.CaseId,
            SourceFileName = body.SourceFileName,
            DocType = body.DocType,
            DocumentDate = body.DocumentDate,
            RawExtraction = body.RawExtraction,
            Purpose = body.Purpose,
        }, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.DocumentEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocBody body, CancellationToken ct)
        => Ok(await _docs.UpdateAsync(id, new UpdateDocumentCommand
        {
            DocType = body.DocType,
            DocumentDate = body.DocumentDate,
            RawExtraction = body.RawExtraction,
        }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.DocumentDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _docs.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/submission-check")]
    public async Task<IActionResult> CheckSubmission(Guid id, CancellationToken ct)
    {
        var result = await _docs.CheckSubmissionAsync(id, ct);
        return result.Ready ? Ok(result) : BadRequest(result);
    }

    [HttpGet("by-company/{companyId:guid}")]
    public async Task<IActionResult> GetByCompany(Guid companyId, CancellationToken ct)
   => Ok(await _docs.GetByCompanyAsync(companyId, ct));
}

public record CreateDocBody(Guid CaseId, string SourceFileName, string DocType,
    string? DocumentDate = null, string? RawExtraction = null, string? Purpose = null);

public record UpdateDocBody(string? DocType = null, string? DocumentDate = null, string? RawExtraction = null);
