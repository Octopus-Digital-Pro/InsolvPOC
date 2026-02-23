namespace Insolvex.Domain.Entities;

public class ScheduledEmail : TenantScopedEntity
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public bool IsSent { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
