namespace Insolvex.Domain.Entities;

/// <summary>
/// Audit log entry. Uses BaseEntity (not TenantScopedEntity) because audit entries
/// can be created for unauthenticated/system-level events with no tenant context.
/// TenantId is nullable — null means system-level or pre-authentication event.
/// </summary>
public class AuditLog : BaseEntity
{
   /// <summary>Optional tenant scope. Null for system-level or pre-auth events.</summary>
   public Guid? TenantId { get; set; }
   public virtual Tenant? Tenant { get; set; }

   /// <summary>Tenant name at the time of logging (denormalized for self-contained record).</summary>
   public string? TenantName { get; set; }

   public string Action { get; set; } = string.Empty;

   /// <summary>Human-readable description of what happened, suitable for legal/compliance review.
   /// Example: "Practician Ioan Popescu a modificat dosarul nr. 123/2025: stadiu schimbat din 'Deschis' în 'Lichidare'."</summary>
   public string Description { get; set; } = string.Empty;

   public Guid? UserId { get; set; }
   public string? UserEmail { get; set; }

   /// <summary>Full name of the user at the time of the action (denormalized).</summary>
   public string? UserFullName { get; set; }

   public string? EntityType { get; set; }
   public Guid? EntityId { get; set; }

   /// <summary>Human-readable name of the entity (case number, company name, document title).</summary>
   public string? EntityName { get; set; }

   /// <summary>Associated case number, if applicable.</summary>
   public string? CaseNumber { get; set; }

   /// <summary>JSON of changed field values (old?new diffs).</summary>
   public string? Changes { get; set; }

   /// <summary>JSON snapshot of old values before mutation.</summary>
   public string? OldValues { get; set; }

   /// <summary>JSON snapshot of new values after mutation.</summary>
   public string? NewValues { get; set; }

   public string? IpAddress { get; set; }
   public string? UserAgent { get; set; }

   /// <summary>HTTP method (GET/POST/PUT/DELETE).</summary>
   public string? RequestMethod { get; set; }

   /// <summary>Request path (e.g. /api/cases/123).</summary>
   public string? RequestPath { get; set; }

   /// <summary>HTTP response status code.</summary>
   public int? ResponseStatusCode { get; set; }

   /// <summary>Request duration in milliseconds.</summary>
   public long? DurationMs { get; set; }

   /// <summary>Audit severity: Info, Warning, Critical.</summary>
   public string Severity { get; set; } = "Info";

   /// <summary>Category for grouping: Auth, Case, Document, Task, Party, Phase, Workflow, Signing, Meeting, Settings, System.</summary>
   public string Category { get; set; } = "System";

   /// <summary>Correlation ID for linking related audit entries.</summary>
   public string? CorrelationId { get; set; }

   public DateTime Timestamp { get; set; }
}
