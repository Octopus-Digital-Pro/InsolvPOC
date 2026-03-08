using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public class InboundEmailProcessorService : IInboundEmailProcessorService
{
  private readonly IApplicationDbContext _db;
  private readonly IFileStorageService _storage;
  private readonly INotificationService _notifications;
  private readonly ILogger<InboundEmailProcessorService> _logger;

  public InboundEmailProcessorService(
    IApplicationDbContext db,
    IFileStorageService storage,
    INotificationService notifications,
    ILogger<InboundEmailProcessorService> logger)
  {
    _db = db;
    _storage = storage;
    _notifications = notifications;
    _logger = logger;
  }

  public async Task<Guid?> ProcessAsync(Stream emlStream, CancellationToken ct = default)
  {
    var message = await MimeMessage.LoadAsync(emlStream, ct);

    // Extract the To address to match against a case
    var recipientAddress = message.To.Mailboxes.FirstOrDefault()?.Address
                        ?? message.Cc?.Mailboxes.FirstOrDefault()?.Address;

    if (string.IsNullOrEmpty(recipientAddress))
    {
      _logger.LogWarning("Inbound email has no To address: {Subject}", message.Subject);
      return null;
    }

    // Match the case by CaseEmailAddress (case-insensitive)
    var insolvencyCase = await _db.InsolvencyCases
      .FirstOrDefaultAsync(c => c.CaseEmailAddress != null
        && c.CaseEmailAddress.ToLower() == recipientAddress.ToLower(), ct);

    if (insolvencyCase is null)
    {
      _logger.LogWarning("No case found for inbound email to {Address}: {Subject}", recipientAddress, message.Subject);
      return null;
    }

    // Extract body (prefer plain text, fall back to HTML)
    var textBody = message.TextBody;
    var htmlBody = message.HtmlBody;
    var body = htmlBody ?? textBody ?? "";
    var isHtml = htmlBody is not null;

    // Extract sender info
    var fromMailbox = message.From.Mailboxes.FirstOrDefault();
    var fromAddress = fromMailbox?.Address ?? "unknown";
    var fromName = fromMailbox?.Name ?? fromAddress;

    // Thread matching: use In-Reply-To or References headers
    Guid? threadId = null;
    Guid? inReplyToId = null;
    var inReplyToHeader = message.InReplyTo;

    if (!string.IsNullOrEmpty(inReplyToHeader))
    {
      var parentEmail = await _db.ScheduledEmails
        .FirstOrDefaultAsync(e => e.ProviderMessageId == inReplyToHeader, ct);
      if (parentEmail is not null)
      {
        inReplyToId = parentEmail.Id;
        threadId = parentEmail.ThreadId ?? parentEmail.Id; // inherit thread or start from parent
      }
    }

    // Process attachments
    var attachments = new List<object>();
    foreach (var attachment in message.Attachments)
    {
      if (attachment is not MimePart part) continue;

      var fileName = part.FileName ?? $"attachment-{Guid.NewGuid():N}.bin";
      var contentType = part.ContentType.MimeType ?? "application/octet-stream";
      var storageKey = $"emails/{insolvencyCase.Id}/{Guid.NewGuid():N}_{fileName}";

      using var ms = new MemoryStream();
      await part.Content.DecodeToAsync(ms, ct);
      ms.Position = 0;
      await _storage.UploadAsync(storageKey, ms, contentType, ct);

      attachments.Add(new { fileName, storageKey, contentType, size = ms.Length });
    }

    // Create the inbound ScheduledEmail record
    var emailId = Guid.NewGuid();
    var scheduledEmail = new ScheduledEmail
    {
      Id = emailId,
      TenantId = insolvencyCase.TenantId,
      CaseId = insolvencyCase.Id,
      To = recipientAddress,
      Subject = message.Subject ?? "(no subject)",
      Body = body,
      IsHtml = isHtml,
      Direction = "Inbound",
      FromName = fromName,
      CaseEmailAddress = insolvencyCase.CaseEmailAddress,
      ProviderMessageId = message.MessageId,
      ThreadId = threadId,
      InReplyToId = inReplyToId,
      AttachmentsJson = attachments.Count > 0 ? JsonSerializer.Serialize(attachments) : null,
      IsSent = true, // inbound emails are already "delivered"
      SentAt = message.Date.UtcDateTime,
      ScheduledFor = message.Date.UtcDateTime,
      Status = "Received",
      CreatedOn = DateTime.UtcNow,
    };

    _db.ScheduledEmails.Add(scheduledEmail);
    await _db.SaveChangesAsync(ct);

    // Create notifications for the assigned practitioner
    if (insolvencyCase.AssignedToUserId.HasValue)
    {
      await _notifications.CreateAsync(new CreateNotificationDto(
        UserId: insolvencyCase.AssignedToUserId.Value,
        Title: $"New email from {fromName}",
        Message: message.Subject ?? "(no subject)",
        Category: "Email",
        RelatedCaseId: insolvencyCase.Id,
        RelatedEmailId: emailId,
        ActionUrl: $"/cases/{insolvencyCase.Id}?tab=emails"
      ), ct);
    }

    _logger.LogInformation(
      "Processed inbound email {EmailId} from {From} to case {CaseId} ({CaseNumber})",
      emailId, fromAddress, insolvencyCase.Id, insolvencyCase.CaseNumber);

    return emailId;
  }
}
