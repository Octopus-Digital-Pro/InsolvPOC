using Microsoft.EntityFrameworkCore;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain;
using Insolvex.Domain.Entities;

namespace Insolvex.API.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DocumentService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
      _currentUser = currentUser;
   _audit = audit;
    }

    public async Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
  var doc = await _db.InsolvencyDocuments
         .FirstOrDefaultAsync(d => d.Id == id && (tenantId == null || d.TenantId == tenantId), ct);
        return doc?.ToDto();
    }

    public async Task<DocumentDto> CreateAsync(CreateDocumentCommand cmd, CancellationToken ct)
    {
     var tenantId = _currentUser.TenantId
     ?? throw new BusinessException("Tenant context is required.");

        if (!await _db.InsolvencyCases.AnyAsync(c => c.Id == cmd.CaseId && c.TenantId == tenantId, ct))
            throw new BusinessException("Case not found within this tenant.");

        var requiresSignature = DocumentTypeRules.RequiresSignature(cmd.DocType);

        var doc = new InsolvencyDocument
        {
         Id = Guid.NewGuid(),
         TenantId = tenantId,
            CaseId = cmd.CaseId,
       SourceFileName = cmd.SourceFileName,
    DocType = cmd.DocType,
            DocumentDate = cmd.DocumentDate,
 UploadedBy = _currentUser.Email ?? "Unknown",
        UploadedAt = DateTime.UtcNow,
 RawExtraction = cmd.RawExtraction,
            RequiresSignature = requiresSignature,
        Purpose = cmd.Purpose,
    CreatedOn = DateTime.UtcNow,
     CreatedBy = _currentUser.Email ?? "System",
        };

        _db.InsolvencyDocuments.Add(doc);
    await _db.SaveChangesAsync(ct);

   await _audit.LogAsync(new AuditEntry
        {
      Action = "Document.Created",
            Description = $"Document '{doc.SourceFileName}' of type '{doc.DocType}' was added to a case.",
            EntityType = "InsolvencyDocument", EntityId = doc.Id, EntityName = doc.SourceFileName,
        Severity = "Info", Category = "DocumentManagement",
        });

    return doc.ToDto();
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentCommand cmd, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var doc = await _db.InsolvencyDocuments
 .FirstOrDefaultAsync(d => d.Id == id && (tenantId == null || d.TenantId == tenantId), ct)
    ?? throw new BusinessException($"Document {id} not found.");

        if (cmd.DocType != null) doc.DocType = cmd.DocType;
        if (cmd.DocumentDate != null) doc.DocumentDate = cmd.DocumentDate;
        if (cmd.RawExtraction != null) doc.RawExtraction = cmd.RawExtraction;
   doc.LastModifiedOn = DateTime.UtcNow;
        doc.LastModifiedBy = _currentUser.Email;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
     Action = "Document.Updated",
         Description = $"Document '{doc.SourceFileName}' was updated.",
      EntityType = "InsolvencyDocument", EntityId = doc.Id, EntityName = doc.SourceFileName,
          Severity = "Info", Category = "DocumentManagement",
        });

     return doc.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var doc = await _db.InsolvencyDocuments
         .FirstOrDefaultAsync(d => d.Id == id && (tenantId == null || d.TenantId == tenantId), ct)
       ?? throw new BusinessException($"Document {id} not found.");

 _db.InsolvencyDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry
        {
          Action = "Document.Deleted",
        Description = $"Document '{doc.SourceFileName}' was permanently deleted.",
 EntityType = "InsolvencyDocument", EntityId = id, EntityName = doc.SourceFileName,
        Severity = "Warning", Category = "DocumentManagement",
        });
    }

    public async Task<SubmissionCheckResult> CheckSubmissionAsync(Guid id, CancellationToken ct)
    {
   var tenantId = _currentUser.TenantId;
        var doc = await _db.InsolvencyDocuments
    .FirstOrDefaultAsync(d => d.Id == id && (tenantId == null || d.TenantId == tenantId), ct)
         ?? throw new BusinessException($"Document {id} not found.");

        if (doc.RequiresSignature && !doc.IsSigned)
            return new SubmissionCheckResult
            {
     Ready = false,
                Message = "This document requires a digital signature before submission. Sign it via Settings ? E-Signing or download, sign externally, and re-upload.",
   };

    return new SubmissionCheckResult { Ready = true, Message = "Document ready for submission." };
    }

    public async Task<List<DocumentDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct)
    {
      var tenantId = _currentUser.TenantId;

        var caseIdsFromParties = await _db.CaseParties
 .Where(p => p.CompanyId == companyId && (tenantId == null || p.TenantId == tenantId))
     .Select(p => p.CaseId).Distinct().ToListAsync(ct);

     var directCaseIds = await _db.InsolvencyCases
            .Where(c => c.CompanyId == companyId && (tenantId == null || c.TenantId == tenantId))
     .Select(c => c.Id).ToListAsync(ct);

  var allCaseIds = caseIdsFromParties.Union(directCaseIds).Distinct().ToList();

   return await _db.InsolvencyDocuments
            .Where(d => allCaseIds.Contains(d.CaseId))
       .OrderByDescending(d => d.UploadedAt)
          .Select(d => d.ToDto())
     .ToListAsync(ct);
    }
}
