using Microsoft.EntityFrameworkCore;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Entities;

namespace Insolvio.Core.Services;

public sealed class TrainingService : ITrainingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IDocumentAiService _ai;

    public TrainingService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IDocumentAiService ai)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _ai = ai;
    }

    public async Task<TrainingDocumentDto> UploadDocumentAsync(
        string documentType, string fileName, Stream fileStream, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var id = Guid.NewGuid();
        var storageKey = $"training/{tenantId}/{id}/{fileName}";
        var contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";

        await _storage.UploadAsync(storageKey, fileStream, contentType, ct);

        var profile = new IncomingDocumentProfile
        {
            Id = id,
            TenantId = tenantId,
            DocumentType = documentType,
            StorageKey = storageKey,
            OriginalFileName = fileName,
            FileSizeBytes = fileStream.CanSeek ? fileStream.Length : 0,
            UploadedOn = DateTime.UtcNow,
            IsActive = true,
        };

        _db.IncomingDocumentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        return ToDto(profile);
    }

    public async Task<(List<TrainingDocumentDto> Items, int Total)> GetDocumentsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.IncomingDocumentProfiles.AsNoTracking()
            .OrderByDescending(p => p.CreatedOn);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new TrainingDocumentDto(
                p.Id,
                p.DocumentType,
                p.OriginalFileName,
                p.AnnotationsJson != null ? "annotated" : "pending",
                (float?)p.AiConfidence,
                p.AiModel,
                p.CreatedOn,
                p.LastModifiedOn))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task SaveAnnotationsAsync(Guid documentId, string annotationsJson, CancellationToken ct = default)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Training document {documentId} not found.");

        profile.AnnotationsJson = annotationsJson;
        profile.LastAnnotatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApproveDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var profile = await _db.IncomingDocumentProfiles
            .FirstOrDefaultAsync(p => p.Id == documentId, ct)
            ?? throw new KeyNotFoundException($"Training document {documentId} not found.");

        // Ensure annotations exist before approval
        if (string.IsNullOrEmpty(profile.AnnotationsJson))
            throw new InvalidOperationException("Cannot approve a document without annotations.");

        profile.IsActive = true;
        profile.LastAnnotatedOn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TrainingStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var total = await _db.IncomingDocumentProfiles.CountAsync(ct);
        var annotated = await _db.IncomingDocumentProfiles
            .CountAsync(p => p.AnnotationsJson != null, ct);
        var pending = total - annotated;

        return new TrainingStatusDto(
            TotalDocuments: total,
            ApprovedDocuments: annotated,
            PendingDocuments: pending,
            CanStartTraining: annotated >= 50,
            CurrentJobStatus: null,
            LastTrainingRun: null);
    }

    private static TrainingDocumentDto ToDto(IncomingDocumentProfile p) => new(
        p.Id,
        p.DocumentType,
        p.OriginalFileName,
        p.AnnotationsJson != null ? "annotated" : "pending",
        (float?)p.AiConfidence,
        p.AiModel,
        p.CreatedOn,
        p.LastModifiedOn);
}
