using Insolvio.Domain.Enums;

namespace Insolvio.Core.DTOs;

// ── Read DTOs ─────────────────────────────────────────────────────────────────

/// <summary>Summary row used in list views.</summary>
public record DocumentTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    DocumentTemplateType TemplateType,
    bool IsSystem,
    bool IsActive,
    string? Stage,
    string? Category,
    bool HasContent,        // true when BodyHtml is not null/empty
    DateTime CreatedOn,
    DateTime? LastModifiedOn
);

/// <summary>Full record including the HTML body — used in the editor.</summary>
public record DocumentTemplateDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DocumentTemplateType TemplateType,
    bool IsSystem,
    bool IsActive,
    string? Stage,
    string? Category,
    string? BodyHtml,
    DateTime CreatedOn,
    DateTime? LastModifiedOn
);

// ── Write DTOs ────────────────────────────────────────────────────────────────

public record CreateDocumentTemplateRequest(
    string Name,
    string? Description,
    string? Category,
    string? BodyHtml
);

public record UpdateDocumentTemplateRequest(
    string Name,
    string? Description,
    string? Category,
    string? BodyHtml,
    bool IsActive
);

/// <summary>Request body for the render endpoint.</summary>
public record RenderTemplateRequest(
    Guid CaseId,
    Guid? RecipientPartyId = null,
    DateTime? PastTasksFromDate = null,
    DateTime? PastTasksToDate = null,
    DateTime? FutureTasksFromDate = null,
    DateTime? FutureTasksToDate = null
);

/// <summary>Result of rendering a template against a case.</summary>
public record RenderTemplateResult(
    string RenderedHtml,
    IReadOnlyDictionary<string, string> MergeData
);
