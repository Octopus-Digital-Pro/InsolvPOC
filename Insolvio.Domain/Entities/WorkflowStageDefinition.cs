namespace Insolvio.Domain.Entities;

/// <summary>
/// Defines a single workflow stage (phase) in an insolvency procedure.
/// Global definitions (TenantId = null) are seeded by the system;
/// tenants can override by creating a record with the same <see cref="StageKey"/>
/// and their TenantId.
/// Resolution: tenant-specific → global fallback.
/// </summary>
public class WorkflowStageDefinition : BaseEntity
{
  /// <summary>Null = global (system default). Non-null = tenant override.</summary>
  public Guid? TenantId { get; set; }
  public virtual Tenant? Tenant { get; set; }

  /// <summary>Stable machine-readable key, e.g. "intake", "claims_collection", "preliminary_table".</summary>
  public string StageKey { get; set; } = string.Empty;

  /// <summary>Display name in the current locale, e.g. "Deschidere procedură".</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Short description of what happens in this stage.</summary>
  public string? Description { get; set; }

  /// <summary>Sort order among stages (0-based).</summary>
  public int SortOrder { get; set; }

  /// <summary>Comma-separated procedure types this stage applies to (null = all).</summary>
  public string? ApplicableProcedureTypes { get; set; }

  // ── Requirements (JSON arrays) ──

  /// <summary>JSON: required case fields, e.g. ["NoticeDate","CourtName"].</summary>
  public string? RequiredFieldsJson { get; set; }

  /// <summary>JSON: required party roles, e.g. ["Debtor","InsolvencyPractitioner"].</summary>
  public string? RequiredPartyRolesJson { get; set; }

  /// <summary>JSON: required document template types, e.g. ["PreliminaryClaimsTable"].</summary>
  public string? RequiredDocTypesJson { get; set; }

  /// <summary>JSON: required task template keys that must be completed.</summary>
  public string? RequiredTaskTemplatesJson { get; set; }

  /// <summary>JSON: additional validation rules (code-based predicates).</summary>
  public string? ValidationRulesJson { get; set; }

  // ── Outputs ──

  /// <summary>JSON: auto-generated document template types when entering this stage.</summary>
  public string? OutputDocTypesJson { get; set; }

  /// <summary>JSON: auto-created task definitions when entering this stage.</summary>
  public string? OutputTasksJson { get; set; }

  /// <summary>JSON: stage keys this stage can transition to.</summary>
  public string? AllowedTransitionsJson { get; set; }

  /// <summary>Whether this stage is active (tenants can deactivate).</summary>
  public bool IsActive { get; set; } = true;

  // ── Navigation ──

  /// <summary>Templates associated with this stage.</summary>
  public ICollection<WorkflowStageTemplate> Templates { get; set; } = new List<WorkflowStageTemplate>();
}
