using Insolvio.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Insolvio.API.BackgroundServices;

/// <summary>
/// Polls the S3 bucket for inbound emails (.eml files deposited by SES),
/// processes them, and moves to a processed/ prefix.
/// </summary>
public class InboundEmailPollingService : BackgroundService
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<InboundEmailPollingService> _logger;
  private const string InboundPrefix = "inbound-emails/";
  private const string ProcessedPrefix = "inbound-emails/processed/";
  private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

  public InboundEmailPollingService(IServiceScopeFactory scopeFactory, ILogger<InboundEmailPollingService> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("InboundEmailPollingService started");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await PollAndProcessAsync(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in InboundEmailPollingService processing loop");
      }

      await Task.Delay(Interval, stoppingToken);
    }

    _logger.LogInformation("InboundEmailPollingService stopped");
  }

  private async Task PollAndProcessAsync(CancellationToken ct)
  {
    using var scope = _scopeFactory.CreateScope();
    var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    var processor = scope.ServiceProvider.GetRequiredService<IInboundEmailProcessorService>();

    var keys = await storage.ListKeysAsync(InboundPrefix, maxKeys: 50, ct);

    if (keys.Count == 0) return;

    _logger.LogInformation("Found {Count} inbound email(s) to process", keys.Count);

    foreach (var key in keys)
    {
      if (ct.IsCancellationRequested) break;

      // Skip the processed subfolder
      if (key.Contains("/processed/")) continue;

      try
      {
        await using var stream = await storage.DownloadAsync(key, ct);
        var emailId = await processor.ProcessAsync(stream, ct);

        // Move to processed prefix
        var fileName = key.Split('/').Last();
        await storage.MoveAsync(key, ProcessedPrefix + fileName, ct);

        _logger.LogInformation("Processed inbound email {Key} → {EmailId}", key, emailId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process inbound email {Key}", key);
        // Move to error prefix to avoid reprocessing
        var fileName = key.Split('/').Last();
        try
        {
          await storage.MoveAsync(key, "inbound-emails/error/" + fileName, ct);
        }
        catch (Exception moveEx)
        {
          _logger.LogError(moveEx, "Failed to move errored email {Key} to error prefix", key);
        }
      }
    }
  }
}
