namespace Insolvio.Core.DTOs;

public record ErrorLogDto(
    Guid Id,
    string Message,
    string? StackTrace,
    string? Source,
    string? RequestPath,
    string? RequestMethod,
    string? UserId,
    string? UserEmail,
    DateTime Timestamp,
    bool IsResolved
);
