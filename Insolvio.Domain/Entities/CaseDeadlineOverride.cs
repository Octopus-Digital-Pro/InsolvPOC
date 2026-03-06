using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Per-case deadline period override with audit trail.
/// Per InsolvencyAppRules section 2: deadline periods can be overridden at case level.
/// Manual override requires reason + audit log.
/// </summary>
public class CaseDeadlineOverride : TenantScopedEntity
{
  public Guid CaseId { get; set; }
  public virtual InsolvencyCase? Case { get; set; }

  /// <summary>The deadline key being overridden (e.g. "ClaimDeadlineDaysFromNotice").</summary>
  public string DeadlineKey { get; set; } = string.Empty;

  /// <summary>Original computed value (days or explicit date, stored as string for flexibility).</summary>
  public string? OriginalValue { get; set; }

  /// <summary>Overridden value.</summary>
  public string OverrideValue { get; set; } = string.Empty;

  /// <summary>Mandatory reason for the override (audit requirement).</summary>
  public string Reason { get; set; } = string.Empty;

  /// <summary>User who applied the override.</summary>
  public Guid? OverriddenByUserId { get; set; }
  public virtual User? OverriddenBy { get; set; }

  /// <summary>When the override was applied.</summary>
  public DateTime OverriddenAt { get; set; }

  /// <summary>Whether the override is currently active.</summary>
  public bool IsActive { get; set; } = true;
}
