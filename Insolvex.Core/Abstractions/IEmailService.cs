namespace Insolvex.Core.Abstractions;

/// <summary>
/// Email sending abstraction. Implementations: SmtpEmailService, SendGridEmailService, etc.
/// </summary>
public interface IEmailService
{
    /// <summary>Send a single email.</summary>
    Task SendAsync(string to, string subject, string body, string? cc = null,
        bool isHtml = true, CancellationToken ct = default);

    /// <summary>Send an email with attachments.</summary>
    Task SendWithAttachmentsAsync(string to, string subject, string body,
      IEnumerable<EmailAttachment> attachments, string? cc = null,
 bool isHtml = true, CancellationToken ct = default);

    /// <summary>Check if the email service is configured and reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public record EmailAttachment(string FileName, byte[] Content, string ContentType = "application/octet-stream");
