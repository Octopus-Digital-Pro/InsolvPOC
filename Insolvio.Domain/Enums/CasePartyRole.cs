namespace Insolvio.Domain.Enums;

/// <summary>
/// Roles a party can play in a Romanian insolvency case (Legea 85/2014).
/// </summary>
public enum CasePartyRole
{
  /// <summary>Debtor / Insolvent company</summary>
  Debtor,

  /// <summary>
  /// Insolvency practitioner — the system user. Kept for backward DB compatibility only.
  /// Do NOT assign new parties with this role; the practitioner is the logged-in tenant.
  /// </summary>
  [Obsolete("The insolvency practitioner is the system user/tenant. Do not use for new party records.")]
  InsolvencyPractitioner,

  /// <summary>Creditor with secured claims (creditor garantat)</summary>
  SecuredCreditor,

  /// <summary>Creditor with unsecured claims (creditor chirografar)</summary>
  UnsecuredCreditor,

  /// <summary>Budgetary creditor (ANAF, local budget)</summary>
  BudgetaryCreditor,

  /// <summary>Employee creditor (salariati)</summary>
  EmployeeCreditor,

  /// <summary>Judge syndic (judecator sindic)</summary>
  JudgeSyndic,

  /// <summary>Court-appointed expert (expert judiciar)</summary>
  CourtExpert,

  /// <summary>Creditors' committee member</summary>
  CreditorsCommittee,

  /// <summary>Special administrator (administrator special)</summary>
  SpecialAdministrator,

  /// <summary>Guarantor / surety</summary>
  Guarantor,

  /// <summary>Third-party stakeholder</summary>
  ThirdParty,
}
