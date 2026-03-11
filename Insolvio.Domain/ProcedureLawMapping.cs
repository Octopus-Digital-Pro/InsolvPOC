namespace Insolvio.Domain;

/// <summary>
/// Maps each <see cref="Enums.ProcedureType"/> to the applicable insolvency law reference.
/// This mapping is system-enforced and must never be overridden by user input.
/// </summary>
public static class ProcedureLawMapping
{
    private static readonly Dictionary<Enums.ProcedureType, string> _map = new()
    {
        [Enums.ProcedureType.Insolventa] = "Legea nr. 85/2014",
        [Enums.ProcedureType.Faliment] = "Legea nr. 85/2014",
        [Enums.ProcedureType.FalimentSimplificat] = "Legea nr. 85/2014",
        [Enums.ProcedureType.Reorganizare] = "Legea nr. 85/2014",
        [Enums.ProcedureType.ConcordatPreventiv] = "Legea nr. 85/2014",
        [Enums.ProcedureType.MandatAdHoc] = "Legea nr. 85/2014",
    };

    /// <summary>
    /// Returns the law reference string for the given procedure type,
    /// or <c>null</c> for <see cref="Enums.ProcedureType.Other"/>.
    /// </summary>
    public static string? GetLaw(Enums.ProcedureType procedureType)
        => _map.TryGetValue(procedureType, out var law) ? law : null;
}
