namespace Insolvex.Core.DTOs;

public record DocumentDto(
    Guid Id,
    Guid CaseId,
    string SourceFileName,
    string DocType,
    string? DocumentDate,
    string UploadedBy,
    DateTime UploadedAt,
    string? RawExtraction,
    bool RequiresSignature = false,
    bool IsSigned = false,
    string? Purpose = null,
    string? Summary = null,
    string? SummaryByLanguageJson = null,
    int? ClassificationConfidence = null,
    string? StorageKey = null,
    string? FileHash = null
);

public record CreateDocumentRequest(
    Guid CaseId,
    string SourceFileName,
    string DocType,
    string? DocumentDate,
    string? RawExtraction,
    string? Purpose = null
);

public record UpdateDocumentRequest(
    string? DocType,
    string? DocumentDate,
    string? RawExtraction
);
