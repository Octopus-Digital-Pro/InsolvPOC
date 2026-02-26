namespace Insolvex.Core.DTOs;

public record EmailDto(
    Guid Id,
 Guid? CaseId,
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    string Body,
    DateTime ScheduledFor,
    DateTime? SentAt,
 bool IsSent,
  string Status,
    int RetryCount,
    string? ErrorMessage,
    string? ProviderMessageId,
    Guid? RelatedTaskId,
    DateTime CreatedOn
);

public record CreateEmailRequest(
    Guid? CaseId,
    string To,
 string? Cc,
    string? Bcc,
    string Subject,
    string Body,
    DateTime? ScheduledFor,
    Guid? RelatedTaskId,
    string? RelatedPartyIdsJson,
    string? RelatedDocumentIdsJson
);

public record ScheduleTemplateEmailRequest(
    Guid CaseId,
    string TemplateType,
    Guid? RecipientPartyId,
  DateTime? ScheduledFor
);
