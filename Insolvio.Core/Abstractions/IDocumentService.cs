using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Domain service for insolvency document management (CRUD, submission checks, company docs).
/// All operations are tenant-scoped and audited.
/// </summary>
public interface IDocumentService
{
  Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task<DocumentDto> CreateAsync(CreateDocumentCommand command, CancellationToken ct = default);
  Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentCommand command, CancellationToken ct = default);
  Task DeleteAsync(Guid id, CancellationToken ct = default);
  Task<SubmissionCheckResult> CheckSubmissionAsync(Guid id, CancellationToken ct = default);
  Task<List<DocumentDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
}

public class CreateDocumentCommand
{
  public Guid CaseId { get; init; }
  public string SourceFileName { get; init; } = string.Empty;
  public string DocType { get; init; } = string.Empty;
  public string? DocumentDate { get; init; }
  public string? RawExtraction { get; init; }
  public string? Purpose { get; init; }
}

public class UpdateDocumentCommand
{
  public string? DocType { get; init; }
  public string? DocumentDate { get; init; }
  public string? RawExtraction { get; init; }
}

public class SubmissionCheckResult
{
  public bool Ready { get; init; }
  public string Message { get; init; } = string.Empty;
}
