using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insolvex.API.BackgroundServices;

/// <summary>
/// Background service that processes pending scheduled emails every 60 seconds.
/// Emails that fail are retried up to 3 times with exponential backoff.
/// </summary>
public class EmailBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;
  private const int MaxRetries = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public EmailBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EmailBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
        _logger.LogInformation("EmailBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
              await ProcessPendingEmailsAsync(stoppingToken);
          }
         catch (Exception ex)
      {
            _logger.LogError(ex, "Error in EmailBackgroundService processing loop");
     }

            await Task.Delay(Interval, stoppingToken);
        }

    _logger.LogInformation("EmailBackgroundService stopped");
    }

    private async Task ProcessPendingEmailsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Get unsent emails that are scheduled for now or past, with retry limit
        var pendingEmails = await db.ScheduledEmails
            .Where(e => !e.IsSent && e.ScheduledFor <= DateTime.UtcNow && e.RetryCount < MaxRetries)
            .OrderBy(e => e.ScheduledFor)
            .Take(20) // Process in batches
 .ToListAsync(ct);

        if (pendingEmails.Count == 0) return;

        _logger.LogInformation("Processing {Count} pending emails", pendingEmails.Count);

  foreach (var email in pendingEmails)
     {
            if (ct.IsCancellationRequested) break;

            try
     {
          await emailService.SendAsync(email.To, email.Subject, email.Body, email.Cc, ct: ct);

    email.IsSent = true;
  email.SentAt = DateTime.UtcNow;
   email.ErrorMessage = null;

             _logger.LogInformation("Email {Id} sent to {To}: {Subject}", email.Id, email.To, email.Subject);
    }
         catch (Exception ex)
{
                email.RetryCount++;
      email.ErrorMessage = ex.Message;

         _logger.LogError(ex, "Failed to send email {Id} to {To} (attempt {Attempt}/{Max})",
         email.Id, email.To, email.RetryCount, MaxRetries);
  }
        }

await db.SaveChangesAsync(ct);
    }
}
