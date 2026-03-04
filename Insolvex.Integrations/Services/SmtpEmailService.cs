using System.Net;
using System.Net.Mail;
using Insolvex.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insolvex.Integrations.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "noreply@insolvex.local";
    public string FromName { get; set; } = "Insolvex";
    public bool EnableSsl { get; set; } = true;
    public bool Enabled { get; set; } = false;
}

public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, string? cc = null,
        bool isHtml = true, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("SMTP not enabled — configure Smtp:Enabled=true in appsettings. Email to {To}: {Subject}", to, subject);
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Enabled=true and provide host/credentials.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml,
        };
        message.To.Add(new MailAddress(to));
        if (!string.IsNullOrWhiteSpace(cc))
            message.CC.Add(new MailAddress(cc));

        using var client = CreateClient();
        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
    }

    public async Task SendWithAttachmentsAsync(string to, string subject, string body,
            IEnumerable<EmailAttachment> attachments, string? cc = null,
            bool isHtml = true, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("SMTP not enabled — configure Smtp:Enabled=true in appsettings. Email to {To}: {Subject}", to, subject);
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Enabled=true and provide host/credentials.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml,
        };
        message.To.Add(new MailAddress(to));
        if (!string.IsNullOrWhiteSpace(cc))
            message.CC.Add(new MailAddress(cc));

        foreach (var att in attachments)
        {
            var ms = new MemoryStream(att.Content);
            message.Attachments.Add(new Attachment(ms, att.FileName, att.ContentType));
        }

        using var client = CreateClient();
        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Email with {Count} attachments sent to {To}: {Subject}",
               message.Attachments.Count, to, subject);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled) return false;
        try
        {
            using var client = CreateClient();
            // SmtpClient doesn't have a Ping — just validate config
            return !string.IsNullOrWhiteSpace(_settings.Host) && _settings.Port > 0;
        }
        catch
        {
            return false;
        }
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        return client;
    }
}
