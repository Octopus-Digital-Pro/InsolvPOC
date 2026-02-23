using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.DocumentView)]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DocumentsController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
    _db = db;
   _currentUser = currentUser;
      _audit = audit;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();
        return Ok(doc.ToDto());
    }

    [HttpPost]
    [RequirePermission(Permission.DocumentUpload)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        // Verify case exists
     var caseExists = await _db.InsolvencyCases.AnyAsync(c => c.Id == request.CaseId);
    if (!caseExists) return BadRequest("Case not found");

     // Auto-detect if signature is required based on doc type
        var requiresSignature = DocumentSigningController.DocTypeRequiresSignature(request.DocType);

     var doc = new InsolvencyDocument
        {
    Id = Guid.NewGuid(),
    CaseId = request.CaseId,
   SourceFileName = request.SourceFileName,
          DocType = request.DocType,
       DocumentDate = request.DocumentDate,
      UploadedBy = _currentUser.Email ?? "Unknown",
            UploadedAt = DateTime.UtcNow,
         RawExtraction = request.RawExtraction,
   RequiresSignature = requiresSignature,
    Purpose = request.Purpose,
        };

        _db.InsolvencyDocuments.Add(doc);
   await _db.SaveChangesAsync();
        await _audit.LogAsync("Document.Created", doc.Id);
     return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc.ToDto());
    }

 [HttpPut("{id:guid}")]
  [RequirePermission(Permission.DocumentEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest request)
    {
      var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

      if (request.DocType != null) doc.DocType = request.DocType;
        if (request.DocumentDate != null) doc.DocumentDate = request.DocumentDate;
        if (request.RawExtraction != null) doc.RawExtraction = request.RawExtraction;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Document.Updated", doc.Id);
        return Ok(doc.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.DocumentDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == id);
      if (doc == null) return NotFound();

        _db.InsolvencyDocuments.Remove(doc);
        await _db.SaveChangesAsync();
      await _audit.LogAsync("Document.Deleted", id);
        return NoContent();
    }

    /// <summary>
    /// Check if a document is ready for submission (signature check).
    /// Returns 200 if ready, 400 if signature required but not signed.
    /// </summary>
    [HttpGet("{id:guid}/submission-check")]
    public async Task<IActionResult> CheckSubmission(Guid id)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

     if (doc.RequiresSignature && !doc.IsSigned)
     return BadRequest(new
      {
      ready = false,
       message = "This document requires a digital signature before submission. Sign it via Settings ? E-Signing or download, sign externally, and re-upload.",
   });

        return Ok(new { ready = true, message = "Document ready for submission" });
    }

    /// <summary>
  /// Get all documents linked to a company (through case parties).
    /// </summary>
    [HttpGet("by-company/{companyId:guid}")]
    public async Task<IActionResult> GetByCompany(Guid companyId)
    {
        var caseIds = await _db.CaseParties
      .Where(p => p.CompanyId == companyId)
            .Select(p => p.CaseId)
        .Distinct()
  .ToListAsync();

        // Also include cases where CompanyId directly references this company
    var directCaseIds = await _db.InsolvencyCases
   .Where(c => c.CompanyId == companyId)
         .Select(c => c.Id)
   .ToListAsync();

        var allCaseIds = caseIds.Union(directCaseIds).Distinct().ToList();

        var docs = await _db.InsolvencyDocuments
       .Where(d => allCaseIds.Contains(d.CaseId))
       .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

  return Ok(docs.Select(d => d.ToDto()).ToList());
    }
}
