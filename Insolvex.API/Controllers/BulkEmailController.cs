using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/bulk-email")]
[Authorize]
[RequirePermission(Permission.EmailCreate)]
public class BulkEmailController : ControllerBase
{
    private readonly IBulkEmailService _bulkEmail;

    public BulkEmailController(IBulkEmailService bulkEmail) => _bulkEmail = bulkEmail;

    [HttpPost("creditor-cohort")]
    public async Task<IActionResult> SendToCreditorCohort(Guid caseId, [FromBody] BulkCreditorEmailBody body, CancellationToken ct)
    {
        var result = await _bulkEmail.SendToCreditorCohortAsync(caseId, new BulkEmailCommand
        {
     Subject = body.Subject, Body = body.Body, Cc = body.Cc, Bcc = body.Bcc,
   IsHtml = body.IsHtml, ScheduledFor = body.ScheduledFor,
      AttachmentsJson = body.AttachmentsJson, RelatedTaskId = body.RelatedTaskId,
    Roles = body.Roles,
        }, ct);
  return Ok(result);
    }

    [HttpGet("creditor-cohort/preview")]
    public async Task<IActionResult> PreviewCohort(Guid caseId, [FromQuery] string? roles, CancellationToken ct)
   => Ok(await _bulkEmail.PreviewCohortAsync(caseId, roles, ct));
}

public record BulkCreditorEmailBody(
    string Subject, string Body,
    string? Cc = null, string? Bcc = null, bool IsHtml = true,
    DateTime? ScheduledFor = null, string? AttachmentsJson = null,
    Guid? RelatedTaskId = null, List<string>? Roles = null);
