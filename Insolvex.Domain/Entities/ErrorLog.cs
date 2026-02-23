namespace Insolvex.Domain.Entities;

public class ErrorLog : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsResolved { get; set; }
}
