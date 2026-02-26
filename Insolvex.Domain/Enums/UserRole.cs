namespace Insolvex.Domain.Enums;

/// <summary>
/// User roles per InsolvencyAppRules section 8.
/// Minimum roles per tenant/company.
/// </summary>
public enum UserRole
{
    GlobalAdmin = 0,
    TenantAdmin = 1,
  /// <summary>Practitioner Partner / Supervisor</summary>
    Partner = 2,
    /// <summary>Legacy alias for Partner</summary>
    Practitioner = 2,
    /// <summary>Case Owner (lead)</summary>
    CaseOwner = 3,
    /// <summary>Case Manager</summary>
    CaseManager = 4,
    /// <summary>Paralegal / Assistant (legacy: Secretary)</summary>
    Assistant = 5,
    /// <summary>Legacy alias for Assistant</summary>
    Secretary = 5,
    /// <summary>Finance role</summary>
    Finance = 6,
    /// <summary>Viewer / Auditor (read-only)</summary>
    Viewer = 7,
}
