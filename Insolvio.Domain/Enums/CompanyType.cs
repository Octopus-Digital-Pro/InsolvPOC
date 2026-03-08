namespace Insolvio.Domain.Enums;

/// <summary>
/// Type of company/entity in the system.
/// </summary>
public enum CompanyType
{
    /// <summary>Debtor / insolvent company being administered</summary>
    Debtor,

    /// <summary>Creditor company</summary>
    Creditor,

    /// <summary>Court / tribunal</summary>
    Court,

    /// <summary>Government agency (ANAF, ONRC, etc.)</summary>
    GovernmentAgency,

    /// <summary>Other third-party entity</summary>
    Other,
}
