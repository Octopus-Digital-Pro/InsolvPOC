namespace Insolvex.Domain.Enums;

/// <summary>
/// User roles per InsolvencyAppRules section 8.
/// Minimum roles per tenant/company.
///
/// NOTE ON DUPLICATE VALUES:
/// System.Text.Json's JsonStringEnumConverter serialises a duplicate-value enum member
/// using the FIRST declared name at that numeric value (after camelCase normalisation).
/// "Practitioner" (2) and "Secretary" (5) are placed first so they round-trip correctly
/// with the frontend. "Partner" and "Assistant" are retained as legacy read aliases.
/// </summary>
public enum UserRole
{
  GlobalAdmin = 0,
  TenantAdmin = 1,
  /// <summary>Practitioner (lead insolvency practitioner). Serialises as "practitioner".</summary>
  Practitioner = 2,
  /// <summary>Legacy alias for Practitioner (Partner / Supervisor).</summary>
  Partner = 2,
  /// <summary>Case Owner (lead)</summary>
  CaseOwner = 3,
  /// <summary>Case Manager</summary>
  CaseManager = 4,
  /// <summary>Secretary / Paralegal. Serialises as "secretary".</summary>
  Secretary = 5,
  /// <summary>Legacy alias for Secretary (Paralegal / Assistant).</summary>
  Assistant = 5,
  /// <summary>Finance role</summary>
  Finance = 6,
  /// <summary>Viewer / Auditor (read-only)</summary>
  Viewer = 7,
}
