using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Domain.Enums;
using System.Text.Json;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/emails")]
[Authorize]
[RequirePermission(Permission.EmailView)]
public class CaseEmailsController : ControllerBase
{
  private readonly ICaseEmailService _emails;
  private readonly IFileStorageService _storage;
  private readonly IAuditService _audit;
  private readonly ICurrentUserService _currentUser;
  private readonly ITaskService _tasks;

  public CaseEmailsController(ICaseEmailService emails, IFileStorageService storage, IAuditService audit, ICurrentUserService currentUser, ITaskService tasks)
  {
    _emails = emails;
    _storage = storage;
    _audit = audit;
    _currentUser = currentUser;
    _tasks = tasks;
  }

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
      To = body.To,
      Cc = body.Cc,
      Bcc = body.Bcc,
      Subject = body.Subject,
      Body = body.Body,
      ScheduledFor = body.ScheduledFor,
      RelatedTaskId = body.RelatedTaskId,
      RelatedPartyIdsJson = body.RelatedPartyIdsJson,
      RelatedDocumentIdsJson = body.RelatedDocumentIdsJson,
    }, ct);
    return CreatedAtAction(nameof(GetCaseEmails), new { caseId }, dto);
  }

  /// <summary>
  /// Compose email to selected parties. Accepts multipart/form-data with optional file uploads
  /// that are saved to document storage.
  /// </summary>
  [HttpPost("compose")]
  [RequirePermission(Permission.EmailCreate)]
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> ComposeEmail(Guid caseId, [FromForm] ComposeEmailForm form, CancellationToken ct)
  {
    var recipientPartyIds = ParseJson<List<Guid>>(form.RecipientPartyIdsJson) ?? new();
    var attachedDocIds = ParseJson<List<Guid>>(form.AttachedDocumentIdsJson) ?? new();

    var uploadedAttachments = new List<object>();
    if (form.Files != null)
    {
      foreach (var file in form.Files.Where(f => f.Length > 0))
      {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        var storageKey = $"emails/{caseId}/{Guid.NewGuid()}_{file.FileName}";
        await _storage.UploadAsync(storageKey, ms, file.ContentType, ct);
        uploadedAttachments.Add(new { fileName = file.FileName, storageKey, contentType = file.ContentType });
        await _audit.LogAsync(new AuditEntry
        {
          Action = "Document Uploaded via Email Compose",
          Description = $"File '{file.FileName}' uploaded and attached to email for case {caseId}.",
          EntityType = "ScheduledEmail", EntityId = caseId, EntityName = file.FileName,
          Severity = "Info", Category = "EmailManagement",
        });
      }
    }

    var dto = await _emails.ComposeAsync(caseId, new ComposeEmailCommand
    {
      RecipientPartyIds = recipientPartyIds,
      ToAddresses = form.ToAddresses,
      Cc = form.Cc,
      Subject = form.Subject ?? string.Empty,
      Body = form.Body ?? string.Empty,
      IsHtml = form.IsHtml,
      RelatedTaskId = form.RelatedTaskId,
      ReplyToEmailId = form.ReplyToEmailId,
      AttachedDocumentIds = attachedDocIds,
      UploadedAttachmentsJson = uploadedAttachments.Count > 0 ? JsonSerializer.Serialize(uploadedAttachments) : null,
      FromName = _currentUser.Email,
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

  [HttpPut("{emailId:guid}/read")]
  public async Task<IActionResult> MarkRead(Guid caseId, Guid emailId, CancellationToken ct)
  {
    await _emails.MarkReadAsync(caseId, emailId, ct);
    return NoContent();
  }

  /// <summary>Create a follow-up task linked to this email.</summary>
  [HttpPost("{emailId:guid}/create-task")]
  [RequirePermission(Permission.TaskCreate)]
  public async Task<IActionResult> CreateTaskFromEmail(
    Guid caseId, Guid emailId, [FromBody] CreateTaskFromEmailBody body, CancellationToken ct)
  {
    var email = await _emails.GetByIdAsync(caseId, emailId, ct);
    if (email is null) return NotFound();

    var task = await _tasks.CreateForCaseAsync(caseId, new CreateTaskCommand
    {
      CompanyId = body.CompanyId,
      CaseId = caseId,
      Title = body.Title ?? $"Reply to: {email.Subject}",
      Description = body.Description ?? $"Follow up on email from {email.FromName ?? email.To}. Subject: {email.Subject}",
      Category = "EmailResponse",
      Deadline = body.Deadline,
      AssignedToUserId = body.AssignedToUserId,
    }, ct);

    return Ok(task);
  }

  private static T? ParseJson<T>(string? json)
  {
    if (string.IsNullOrWhiteSpace(json)) return default;
    try { return JsonSerializer.Deserialize<T>(json); } catch { return default; }
  }
}

public record ScheduleEmailBody(
    string To, string Subject, string Body,
    string? Cc = null, string? Bcc = null, DateTime? ScheduledFor = null,
    Guid? RelatedTaskId = null, string? RelatedPartyIdsJson = null,
    string? RelatedDocumentIdsJson = null);

public class ComposeEmailForm
{
  public string? RecipientPartyIdsJson { get; set; }
  public string? ToAddresses { get; set; }
  public string? Cc { get; set; }
  public string? Subject { get; set; }
  public string? Body { get; set; }
  public bool IsHtml { get; set; } = true;
  public Guid? RelatedTaskId { get; set; }
  public Guid? ReplyToEmailId { get; set; }
  public string? AttachedDocumentIdsJson { get; set; }
  public List<IFormFile>? Files { get; set; }
}

public record CreateTaskFromEmailBody(
  Guid CompanyId,
  string? Title = null,
  string? Description = null,
  DateTime? Deadline = null,
  Guid? AssignedToUserId = null);
