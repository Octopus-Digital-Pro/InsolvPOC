using Insolvio.Core.DTOs;

namespace Insolvio.Core.Abstractions;

public interface ITrainingService
{
    Task<TrainingDocumentDto> UploadDocumentAsync(string documentType, string fileName, Stream fileStream, CancellationToken ct = default);
    Task<(List<TrainingDocumentDto> Items, int Total)> GetDocumentsAsync(int page, int pageSize, CancellationToken ct = default);
    Task SaveAnnotationsAsync(Guid documentId, string annotationsJson, CancellationToken ct = default);
    Task ApproveDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<TrainingStatusDto> GetStatusAsync(CancellationToken ct = default);
}
