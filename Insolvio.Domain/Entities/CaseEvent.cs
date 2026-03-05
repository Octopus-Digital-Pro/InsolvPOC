using Insolvio.Domain.Enums;

namespace Insolvio.Domain.Entities;

/// <summary>
/// Immutable audit-quality timeline entry for everything that happens on a case.
/// Every action, document upload, task change, phase transition, and deadline
/// produces one CaseEvent. These feed directly into the AI case-status summary.
/// </summary>
public class CaseEvent : TenantScopedEntity
{
    // ── Case link ──────────────────────────────────────────────────────────
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

    // ── What kind of event ─────────────────────────────────────────────────
    /// <summary>
    /// Category: Document | Task | Phase | Party | Calendar | Deadline |
    ///           Communication | Signing | AI | System
    /// </summary>
    public string Category { get; set; } = "System";

    /// <summary>
    /// Specific event type within the category, e.g. "Document.Uploaded",
    /// "Task.Completed", "Phase.Advanced", "Deadline.Missed", "AI.Summary".
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    // ── Human-readable description (localised by AuditService) ─────────────
    /// <summary>
    /// Short, human-readable summary of what happened.
    /// e.g. "Ion Popescu uploaded Notificare creditori (PDF, 2.1 MB)"
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // ── When ───────────────────────────────────────────────────────────────
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // ── Who ────────────────────────────────────────────────────────────────
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }

    // ── Linked database objects ────────────────────────────────────────────
    /// <summary>Type name of the primary linked entity, e.g. "InsolvencyDocument", "CompanyTask", "CasePhase".</summary>
    public string? LinkedEntityType { get; set; }

    /// <summary>PK of the primary linked entity.</summary>
    public Guid? LinkedEntityId { get; set; }

    // ── Parties involved ───────────────────────────────────────────────────
    /// <summary>
    /// JSON array of party references: [{ "id": "...", "name": "...", "role": "..." }]
    /// Populated for party-relevant events (notifications, hearings, distributions).
    /// </summary>
    public string? InvolvedPartiesJson { get; set; }

    // ── Key dates mentioned in / produced by this event ────────────────────
    /// <summary>
    /// JSON array of important dates extracted: [{ "date": "...", "meaning": "..." }]
    /// </summary>
    public string? KeyDatesJson { get; set; }

    // ── Document AI summary (filled when Category = "Document") ────────────
    /// <summary>AI-generated summary of the linked document.</summary>
    public string? DocumentSummary { get; set; }

    /// <summary>AI-extracted actions / obligations from the document.</summary>
    public string? ExtractedActionsJson { get; set; }

    // ── Severity / importance ──────────────────────────────────────────────
    /// <summary>Info | Warning | Critical</summary>
    public string Severity { get; set; } = "Info";

    // ── Metadata for AI consumption ────────────────────────────────────────
    /// <summary>
    /// Arbitrary JSON payload with structured detail for the AI summariser:
    /// deadlines, amounts, document types, task names, etc.
    /// </summary>
    public string? MetadataJson { get; set; }
}
