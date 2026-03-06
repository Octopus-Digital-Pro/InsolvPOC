using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Tracks a rendered document generated from a template via mail-merge.
/// Links template ? rendered output ? delivery status.
/// Per InsolvencyAppRules section 7: Templates-Ro mail merge + email + tracking.
/// </summary>
public class GeneratedLetter : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  /// <summary>Reference to the template used for generation.</summary>
  public Guid? TemplateId { get; set; }
  public virtual DocumentTemplate? Template { get; set; }

  /// <summary>Template type that was rendered.</summary>
  public DocumentTemplateType TemplateType { get; set; }

  /// <summary>Storage key of the rendered output document.</summary>
  public string StorageKey { get; set; } = string.Empty;

  /// <summary>Output filename.</summary>
  public string FileName { get; set; } = string.Empty;

  /// <summary>MIME content type of the rendered document.</summary>
  public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

  /// <summary>File size in bytes.</summary>
  public long FileSizeBytes { get; set; }

  /// <summary>SHA-256 hash of the rendered output for integrity.</summary>
  public string? FileHash { get; set; }

  /// <summary>JSON of the merge field values used for rendering (audit trail).</summary>
  public string? MergeDataJson { get; set; }

  /// <summary>When the document was rendered.</summary>
  public DateTime RenderedAt { get; set; }

  /// <summary>When the document was sent to recipients.</summary>
  public DateTime? SentAt { get; set; }

  /// <summary>Delivery status: Pending, Sent, Failed, Cancelled.</summary>
  public string DeliveryStatus { get; set; } = "Pending";

  /// <summary>Error message if generation or sending failed.</summary>
  public string? ErrorMessage { get; set; }

  /// <summary>Whether this is a critical document that cannot miss its deadline.</summary>
  public bool IsCritical { get; set; }

  /// <summary>Deadline by which this document must be sent.</summary>
  public DateTime? SendDeadline { get; set; }

  /// <summary>ID of the related task (generation task or send task).</summary>
  public Guid? RelatedTaskId { get; set; }

  /// <summary>ID of the scheduled email used to deliver this letter.</summary>
  public Guid? RelatedEmailId { get; set; }

  /// <summary>JSON array of recipient party IDs.</summary>
  public string? RecipientPartyIdsJson { get; set; }
}
