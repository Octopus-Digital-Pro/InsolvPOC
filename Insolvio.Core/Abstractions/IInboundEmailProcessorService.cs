namespace Insolvio.Core.Abstractions;

public interface IInboundEmailProcessorService
{
  /// <summary>
  /// Parse a raw .eml email stream, match it to a case, and create an inbound ScheduledEmail record.
  /// Returns the ID of the created ScheduledEmail, or null if the email couldn't be matched to a case.
  /// </summary>
  Task<Guid?> ProcessAsync(Stream emlStream, CancellationToken ct = default);
}
