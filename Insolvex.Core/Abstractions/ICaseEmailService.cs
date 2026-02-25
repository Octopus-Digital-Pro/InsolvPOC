using Insolvex.Core.DTOs;

namespace Insolvex.Core.Abstractions;

/// <summary>
/// Domain service for case-scoped email management and bulk creditor email operations.
/// Per InsolvencyAppRules: emails linked to tasks, parties, documents with delivery proof.
/// </summary>
public interface ICaseEmailService
{
  Task<List<EmailDto>> GetByCaseAsync(Guid caseId, string? status = null, bool? sentOnly = null, CancellationToken ct = default);
  Task<CaseEmailSummaryResult> GetSummaryAsync(Guid caseId, CancellationToken ct = default);
  Task<EmailDto> ScheduleAsync(Guid caseId, ScheduleEmailCommand command, CancellationToken ct = default);
  Task CancelAsync(Guid caseId, Guid emailId, CancellationToken ct = default);
}

/// <summary>
/// Bulk email operations: send to creditor cohorts.
/// </summary>
public interface IBulkEmailService
{
  Task<BulkEmailResult> SendToCreditorCohortAsync(Guid caseId, BulkEmailCommand command, CancellationToken ct = default);
  Task<CohortPreviewResult> PreviewCohortAsync(Guid caseId, string? roles = null, CancellationToken ct = default);
}

// ?? Commands & Results ??

public class ScheduleEmailCommand
{
  public string To { get; init; } = string.Empty;
  public string? Cc { get; init; }
  public string? Bcc { get; init; }
  public string Subject { get; init; } = string.Empty;
  public string Body { get; init; } = string.Empty;
  public DateTime? ScheduledFor { get; init; }
  public Guid? RelatedTaskId { get; init; }
  public string? RelatedPartyIdsJson { get; init; }
  public string? RelatedDocumentIdsJson { get; init; }
}

public class BulkEmailCommand
{
  public string Subject { get; init; } = string.Empty;
  public string Body { get; init; } = string.Empty;
  public string? Cc { get; init; }
  public string? Bcc { get; init; }
  public bool IsHtml { get; init; } = true;
  public DateTime? ScheduledFor { get; init; }
  public string? AttachmentsJson { get; init; }
  public Guid? RelatedTaskId { get; init; }
  public List<string>? Roles { get; init; }
}

public class BulkEmailResult
{
  public int EmailsScheduled { get; init; }
  public DateTime ScheduledFor { get; init; }
  public List<RecipientInfo> Recipients { get; init; } = new();
}

public class RecipientInfo
{
  public Guid PartyId { get; init; }
  public string? Name { get; init; }
  public string? Email { get; init; }
  public string Role { get; init; } = string.Empty;
}

public class CohortPreviewResult
{
  public int Total { get; init; }
  public int WithEmail { get; init; }
  public int WithoutEmail { get; init; }
  public List<CohortRecipient> Recipients { get; init; } = new();
}

public class CohortRecipient
{
  public Guid PartyId { get; init; }
  public string? Name { get; init; }
  public string? Email { get; init; }
  public string Role { get; init; } = string.Empty;
  public bool HasEmail { get; init; }
}

public class CaseEmailSummaryResult
{
  public int Total { get; init; }
  public int Sent { get; init; }
  public int Pending { get; init; }
  public int Failed { get; init; }
  public int Scheduled { get; init; }
}
