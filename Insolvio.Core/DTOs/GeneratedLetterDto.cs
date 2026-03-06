using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

public record GeneratedLetterDto(
    Guid Id,
    Guid CaseId,
    Guid? TemplateId,
    string TemplateType,
    string StorageKey,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime RenderedAt,
    DateTime? SentAt,
    string DeliveryStatus,
    string? ErrorMessage,
    bool IsCritical,
    DateTime? SendDeadline,
    DateTime CreatedOn
);
