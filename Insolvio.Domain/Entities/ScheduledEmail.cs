namespace Insolvio.Domain.Entities;

/// <summary>
/// Per InsolvencyAppRules: Email entity linked to case, tasks, parties, documents.
/// Tracks scheduled/sent emails with delivery proof.
/// </summary>
public class ScheduledEmail : TenantScopedEntity
{
  /// <summary>Link to the insolvency case (nullable for system-level emails).</summary>
  public Guid? CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  public string To { get; set; } = string.Empty;
  public string? Cc { get; set; }
  public string? Bcc { get; set; }
  public string Subject { get; set; } = string.Empty;
  public string Body { get; set; } = string.Empty;

  /// <summary>JSON array of attachment info [{fileName, storageKey, contentType}].</summary>
  public string? AttachmentsJson { get; set; }

  public DateTime ScheduledFor { get; set; }
  public DateTime? SentAt { get; set; }
  public bool IsSent { get; set; }
  public int RetryCount { get; set; }
  public string? ErrorMessage { get; set; }

  /// <summary>Provider-assigned message ID for tracking (e.g., SendGrid message ID).</summary>
  public string? ProviderMessageId { get; set; }

  /// <summary>Delivery status: Draft, Scheduled, Sending, Sent, Failed, Cancelled.</summary>
  public string Status { get; set; } = "Scheduled";

  /// <summary>Whether this is an HTML email.</summary>
  public bool IsHtml { get; set; } = true;

  // ?? Linked entities ??

  /// <summary>Related task ID (for email send tasks).</summary>
  public Guid? RelatedTaskId { get; set; }

  /// <summary>JSON array of related party IDs.</summary>
  public string? RelatedPartyIdsJson { get; set; }

  /// <summary>JSON array of related document IDs.</summary>
  public string? RelatedDocumentIdsJson { get; set; }

  /// <summary>Related generated letter ID (if this email delivers a template output).</summary>
  public Guid? RelatedGeneratedLetterId { get; set; }

  // ?? Threading ??

  /// <summary>Groups related emails into a conversation thread. Null = standalone.</summary>
  public Guid? ThreadId { get; set; }

  /// <summary>ID of the email this is replying to (for threaded display).</summary>
  public Guid? InReplyToId { get; set; }

  /// <summary>Direction: "Outbound" (sent by practitioner) or "Inbound" (received reply). Default Outbound.</summary>
  public string Direction { get; set; } = "Outbound";

  /// <summary>Sender display name shown in the From field.</summary>
  public string? FromName { get; set; }

  /// <summary>Per-case email address (e.g. case-12345@insolvio.ro).</summary>
  public string? CaseEmailAddress { get; set; }

  /// <summary>Whether this inbound email has been read by the assigned user.</summary>
  public bool IsRead { get; set; }
}
