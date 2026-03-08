namespace Insolvio.Core.DTOs;

public record TrainingDocumentDto(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string? ReviewStatus,
    float? AiConfidence,
    string? AiModel,
    DateTime CreatedOn,
    DateTime? LastModifiedOn);

public record TrainingStatusDto(
    int TotalDocuments,
    int ApprovedDocuments,
    int PendingDocuments,
    bool CanStartTraining,
    string? CurrentJobStatus,
    DateTime? LastTrainingRun);
