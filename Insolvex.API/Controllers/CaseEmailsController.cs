using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/emails")]
[Authorize]
[RequirePermission(Permission.EmailView)]
public class CaseEmailsController : ControllerBase
{
    private readonly ICaseEmailService _emails;

    public CaseEmailsController(ICaseEmailService emails) => _emails = emails;

    [HttpGet]
    public async Task<IActionResult> GetCaseEmails(
 Guid caseId, [FromQuery] string? status = null, [FromQuery] bool? sentOnly = null, CancellationToken ct = default)
  => Ok(await _emails.GetByCaseAsync(caseId, status, sentOnly, ct));

    [HttpGet("summary")]
    public async Task<IActionResult> GetEmailSummary(Guid caseId, CancellationToken ct)
   => Ok(await _emails.GetSummaryAsync(caseId, ct));

    [HttpPost]
    [RequirePermission(Permission.EmailCreate)]
  public async Task<IActionResult> ScheduleEmail(Guid caseId, [FromBody] ScheduleEmailBody body, CancellationToken ct)
    {
        var dto = await _emails.ScheduleAsync(caseId, new ScheduleEmailCommand
   {
       To = body.To, Cc = body.Cc, Bcc = body.Bcc,
  Subject = body.Subject, Body = body.Body,
      ScheduledFor = body.ScheduledFor, RelatedTaskId = body.RelatedTaskId,
  RelatedPartyIdsJson = body.RelatedPartyIdsJson,
   RelatedDocumentIdsJson = body.RelatedDocumentIdsJson,
      }, ct);
    return CreatedAtAction(nameof(GetCaseEmails), new { caseId }, dto);
    }

  [HttpDelete("{emailId:guid}")]
    [RequirePermission(Permission.EmailDelete)]
    public async Task<IActionResult> CancelEmail(Guid caseId, Guid emailId, CancellationToken ct)
    {
    await _emails.CancelAsync(caseId, emailId, ct);
        return Ok(new { message = "Email cancelled." });
    }
}

public record ScheduleEmailBody(
    string To, string Subject, string Body,
    string? Cc = null, string? Bcc = null, DateTime? ScheduledFor = null,
    Guid? RelatedTaskId = null, string? RelatedPartyIdsJson = null,
    string? RelatedDocumentIdsJson = null);
