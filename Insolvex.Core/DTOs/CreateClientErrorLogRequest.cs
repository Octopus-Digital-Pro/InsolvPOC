namespace Insolvex.Core.DTOs;

public record CreateClientErrorLogRequest(
    string Message,
    string? StackTrace,
    string? Source,
    string? RequestPath,
    string? UserAgent,
    string? AdditionalContext
);
