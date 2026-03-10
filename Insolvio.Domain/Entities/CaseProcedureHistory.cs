using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Immutable record of every procedure type change on an insolvency case.
/// Created by ProcedureTypeChangeService; never updated after insert.
/// </summary>
public class CaseProcedureHistory : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Guid? ChangedByUserId { get; set; }
    public virtual User? ChangedBy { get; set; }

    /// <summary>Procedure type before the change.</summary>
    public ProcedureType OldProcedureType { get; set; }

    /// <summary>Procedure type after the change.</summary>
    public ProcedureType NewProcedureType { get; set; }

    /// <summary>Mandatory user-supplied reason for the change (max 2000 chars).</summary>
    public string? Reason { get; set; }

    /// <summary>JSON array of stage keys that were removed because they were not yet started.</summary>
    public string? WorkflowStagesRemovedJson { get; set; }
}
