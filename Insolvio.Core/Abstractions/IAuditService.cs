namespace Insolvio.Core.Abstractions;

public interface IAuditService
{
  /// <summary>Simple action log (backward-compatible).</summary>
  Task LogAsync(string action, Guid? entityId = null, object? changes = null);

  /// <summary>Detailed audit log with entity type, old/new values, severity, and category.</summary>
  Task LogAsync(AuditEntry entry);

  /// <summary>Log an entity CRUD operation with automatic old?new diffing.</summary>
  Task LogEntityAsync(string action, string entityType, Guid entityId,
      object? oldValues = null, object? newValues = null,
string severity = "Info", string? correlationId = null);

  /// <summary>Log an authentication event (login, logout, password change, failed attempt).</summary>
  Task LogAuthAsync(string action, string? email = null, Guid? userId = null, string severity = "Info");

  /// <summary>Log a workflow/stage transition event.</summary>
  Task LogWorkflowAsync(string action, Guid caseId, object? details = null, string severity = "Info");

  /// <summary>Log a document signing event.</summary>
  Task LogSigningAsync(string action, Guid documentId, object? details = null, string severity = "Info");
}

/// <summary>Structured audit entry for detailed logging.</summary>
public class AuditEntry
{
  public string Action { get; set; } = string.Empty;
  public string? Description { get; set; }
  public string? EntityType { get; set; }
  public Guid? EntityId { get; set; }
  public string? EntityName { get; set; }
  public string? CaseNumber { get; set; }
  public object? OldValues { get; set; }
  public object? NewValues { get; set; }
  public object? Changes { get; set; }
  public string Severity { get; set; } = "Info";
  public string Category { get; set; } = "System";
  public string? CorrelationId { get; set; }
}
