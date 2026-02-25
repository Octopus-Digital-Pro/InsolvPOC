namespace Insolvex.Domain.Enums;

/// <summary>
/// Phases of a Romanian insolvency procedure (Legea 85/2014).
/// Each case progresses through these phases sequentially.
/// </summary>
public enum PhaseType
{
  /// <summary>Cerere de deschidere a procedurii</summary>
  OpeningRequest,

  /// <summary>Deschiderea procedurii de insolventa (perioada de observatie)</summary>
  ObservationPeriod,

  /// <summary>Notificarea creditorilor si publicarea in BPI</summary>
  CreditorNotification,

  /// <summary>Depunerea declaratiilor de creanta</summary>
  ClaimsFiling,

  /// <summary>Verificarea si intocmirea tabelului preliminar de creante</summary>
  PreliminaryClaimsTable,

  /// <summary>Contestatii la tabelul preliminar</summary>
  ClaimsContestations,

  /// <summary>Tabelul definitiv de creante</summary>
  DefinitiveClaimsTable,

  /// <summary>Raportul asupra cauzelor si imprejurarilor insolventei (Art. 97)</summary>
  CausesReport,

  /// <summary>Propunerea planului de reorganizare</summary>
  ReorganizationPlanProposal,

  /// <summary>Votarea planului de reorganizare</summary>
  ReorganizationPlanVoting,

  /// <summary>Confirmarea planului de reorganizare</summary>
  ReorganizationPlanConfirmation,

  /// <summary>Implementarea planului de reorganizare</summary>
  ReorganizationExecution,

  /// <summary>Trecerea la faliment (conversie)</summary>
  BankruptcyConversion,

  /// <summary>Lichidarea activelor</summary>
  AssetLiquidation,

  /// <summary>Distribuirea sumelor catre creditori</summary>
  CreditorDistribution,

  /// <summary>Raportul final</summary>
  FinalReport,

  /// <summary>Inchiderea procedurii</summary>
  ProcedureClosure,
}
