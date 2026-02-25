namespace Insolvex.Core.DTOs;

public record SigningKeyDto(
    Guid Id,
    string Name,
    string? SubjectName,
    string? IssuerName,
    string? Thumbprint,
    string? SerialNumber,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    bool IsExpired,
    DateTime? LastUsedAt,
    DateTime CreatedOn
);

public record KeyStatusDto(bool HasKey, bool CanSign, SigningKeyDto? Key);

public record SignatureDto(
    Guid Id,
    Guid DocumentId,
    string? DocumentName,
    Guid SignedByUserId,
    string? SignedByEmail,
    DateTime SignedAt,
    string DocumentHash,
    string? CertificateSubject,
    string? CertificateThumbprint,
    string? CertificateSerialNumber,
    bool? IsValid,
    DateTime? VerifiedAt,
    string? Reason
);

public record UnifiedCalendarItem(
    Guid Id,
    string Title,
    DateTime Start,
    DateTime? End,
    string Type,
    string? SubType,
    bool IsCritical,
    bool IsCancelled,
    string? Status
);
