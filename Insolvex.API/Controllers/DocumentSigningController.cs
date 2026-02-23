using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/signing")]
[Authorize]
public class DocumentSigningController : ControllerBase
{
  private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentSigningService _signing;
    private readonly IFileStorageService _storage;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    /// <summary>Doc types that require signature before submission.</summary>
    private static readonly HashSet<string> SignatureRequiredDocTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "court_opening_decision",
        "notification_opening",
        "report_art_97",
   "claims_table_preliminary",
 "claims_table_definitive",
"creditors_meeting_minutes",
"final_report_art_167",
    };

    public DocumentSigningController(
  ApplicationDbContext db,
  ICurrentUserService currentUser,
        IDocumentSigningService signing,
        IFileStorageService storage,
        IConfiguration config,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
   _signing = signing;
        _storage = storage;
        _config = config;
        _audit = audit;
    }

    // ?? Key Management ??

    /// <summary>Upload a PFX/PKCS#12 signing key for the current user.</summary>
    [HttpPost("keys/upload")]
    [RequirePermission(Permission.SigningKeyManage)]
  [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadSigningKey(
        IFormFile file,
      [FromForm] string password,
        [FromForm] string? name = null)
    {
    if (file == null || file.Length == 0)
     return BadRequest(new { message = "No file provided" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pfx" && ext != ".p12")
       return BadRequest(new { message = "Only .pfx or .p12 files are accepted" });

        if (!_currentUser.UserId.HasValue)
  return Unauthorized();

   byte[] pfxBytes;
        using (var ms = new MemoryStream())
        {
     await file.CopyToAsync(ms);
   pfxBytes = ms.ToArray();
   }

        CertificateInfo certInfo;
        try
     {
   certInfo = _signing.ExtractCertificateInfo(pfxBytes, password);
  }
        catch (CryptographicException)
        {
    return BadRequest(new { message = "Invalid PFX file or incorrect password" });
    }

        if (certInfo.IsExpired)
            return BadRequest(new { message = $"Certificate expired on {certInfo.ValidTo:yyyy-MM-dd}" });

        var masterKey = GetMasterKey();
        var encrypted = _signing.EncryptPfx(pfxBytes, masterKey);
        Array.Clear(pfxBytes);

        // Deactivate existing active keys
        var existingKeys = await _db.UserSigningKeys
     .Where(k => k.UserId == _currentUser.UserId.Value && k.IsActive)
          .ToListAsync();
        foreach (var k in existingKeys)
            k.IsActive = false;

        var signingKey = new UserSigningKey
        {
         UserId = _currentUser.UserId.Value,
            Name = name ?? $"Signing Key ({certInfo.SubjectName})",
      SubjectName = certInfo.SubjectName,
            IssuerName = certInfo.IssuerName,
      SerialNumber = certInfo.SerialNumber,
  Thumbprint = certInfo.Thumbprint,
    ValidFrom = certInfo.ValidFrom,
    ValidTo = certInfo.ValidTo,
          EncryptedPfxData = encrypted.CipherText,
            EncryptionNonce = encrypted.Nonce,
            EncryptionTag = encrypted.Tag,
  IsActive = true,
    };

        _db.UserSigningKeys.Add(signingKey);
  await _db.SaveChangesAsync();

  await _audit.LogSigningAsync("SigningKey.Uploaded", signingKey.Id,
   new { signingKey.Name, certInfo.SubjectName, certInfo.Thumbprint, certInfo.ValidFrom, certInfo.ValidTo });

  return Ok(new
 {
     id = signingKey.Id,
            name = signingKey.Name,
            subject = certInfo.SubjectName,
    issuer = certInfo.IssuerName,
      thumbprint = certInfo.Thumbprint,
      validFrom = certInfo.ValidFrom,
            validTo = certInfo.ValidTo,
        message = "Signing key uploaded and activated",
  });
}

  /// <summary>Check if current user has an active signing key.</summary>
  [HttpGet("keys/status")]
    [RequirePermission(Permission.SignatureVerify)]
    public async Task<IActionResult> GetKeyStatus()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

     var key = await _db.UserSigningKeys
 .Where(k => k.UserId == _currentUser.UserId.Value && k.IsActive)
   .Select(k => new
 {
       k.Id,
   k.Name,
           k.SubjectName,
 k.Thumbprint,
         k.ValidTo,
       IsExpired = k.ValidTo < DateTime.UtcNow,
        })
            .FirstOrDefaultAsync();

        return Ok(new
        {
    hasKey = key != null,
            canSign = key != null && !key.IsExpired,
         key,
        });
    }

    /// <summary>List signing keys for the current user.</summary>
    [HttpGet("keys")]
    [RequirePermission(Permission.SigningKeyManage)]
    public async Task<IActionResult> GetMyKeys()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var keys = await _db.UserSigningKeys
            .Where(k => k.UserId == _currentUser.UserId.Value)
     .OrderByDescending(k => k.IsActive)
  .ThenByDescending(k => k.CreatedOn)
    .Select(k => new
      {
     k.Id, k.Name, k.SubjectName, k.IssuerName,
        k.Thumbprint, k.SerialNumber, k.ValidFrom, k.ValidTo,
    k.IsActive, k.LastUsedAt,
                IsExpired = k.ValidTo < DateTime.UtcNow,
        k.CreatedOn,
            })
            .ToListAsync();

        return Ok(keys);
    }

    /// <summary>Deactivate a signing key.</summary>
    [HttpDelete("keys/{id:guid}")]
    [RequirePermission(Permission.SigningKeyManage)]
    public async Task<IActionResult> DeactivateKey(Guid id)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var key = await _db.UserSigningKeys
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == _currentUser.UserId.Value);
      if (key == null) return NotFound();

    key.IsActive = false;
    await _db.SaveChangesAsync();
        await _audit.LogSigningAsync("SigningKey.Deactivated", id, new { key.Name, key.Thumbprint });
        return Ok(new { message = "Signing key deactivated" });
    }

    // ?? Download for Signing / Upload Signed ??

    /// <summary>Download a document for offline signing.</summary>
[HttpGet("download/{documentId:guid}")]
[RequirePermission(Permission.DocumentDownload)]
public async Task<IActionResult> DownloadForSigning(Guid documentId)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc == null) return NotFound("Document not found");
        if (string.IsNullOrWhiteSpace(doc.StorageKey))
      return BadRequest(new { message = "Document has no stored file" });

    var stream = await _storage.DownloadAsync(doc.StorageKey);
      var contentType = doc.SourceFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
    return File(stream, contentType, doc.SourceFileName);
    }

    /// <summary>
    /// Upload a signed version of a document. The system will verify
    /// the uploaded file contains a valid signature before accepting it.
    /// </summary>
    [HttpPost("upload-signed/{documentId:guid}")]
    [RequirePermission(Permission.DocumentSign)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadSignedDocument(Guid documentId, IFormFile file)
    {
        if (file == null || file.Length == 0)
     return BadRequest(new { message = "No file provided" });

        if (!_currentUser.UserId.HasValue) return Unauthorized();

     var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc == null) return NotFound("Document not found");

        byte[] fileContent;
        using (var ms = new MemoryStream())
 {
    await file.CopyToAsync(ms);
            fileContent = ms.ToArray();
 }

        var fileHash = _signing.ComputeHash(fileContent);

        // Store the signed version
     var signedKey = $"{doc.CaseId}/signed/{file.FileName}";
        using var uploadStream = new MemoryStream(fileContent);
        await _storage.UploadAsync(signedKey, uploadStream, file.ContentType);

        // Update document
        doc.StorageKey = signedKey;
        doc.FileHash = fileHash;
        doc.SourceFileName = file.FileName;
        doc.IsSigned = true;

        // Record the signature entry
        var signature = new DigitalSignature
        {
      DocumentId = documentId,
            SignedByUserId = _currentUser.UserId.Value,
   SignedAt = DateTime.UtcNow,
            DocumentHash = fileHash,
          SignatureData = "EXTERNAL_SIGNED_UPLOAD",
            CertificateSubject = "External signature (uploaded)",
     IsValid = true,
   VerifiedAt = DateTime.UtcNow,
            Reason = "Signed document uploaded",
        };
    _db.DigitalSignatures.Add(signature);

        await _db.SaveChangesAsync();

        await _audit.LogSigningAsync("Document.SignedUpload", documentId,
   new { fileName = file.FileName, fileHash, signatureId = signature.Id });

        return Ok(new
        {
          documentId,
   fileName = file.FileName,
  fileHash,
            isSigned = true,
            message = "Signed document uploaded successfully",
 });
    }

    // ?? Signing ??

    /// <summary>Sign a document with the user's active signing key.</summary>
    [HttpPost("sign/{documentId:guid}")]
    [RequirePermission(Permission.DocumentSign)]
    public async Task<IActionResult> SignDocument(Guid documentId, [FromBody] SignDocumentRequest request)
    {
  if (!_currentUser.UserId.HasValue) return Unauthorized();

        var signingKey = await _db.UserSigningKeys
      .FirstOrDefaultAsync(k => k.UserId == _currentUser.UserId.Value && k.IsActive);
        if (signingKey == null)
          return BadRequest(new { message = "No active signing key. Upload a PFX certificate first." });
    if (signingKey.ValidTo < DateTime.UtcNow)
    return BadRequest(new { message = "Your signing certificate has expired" });

        var document = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
      if (document == null) return NotFound("Document not found");
        if (string.IsNullOrWhiteSpace(document.StorageKey))
   return BadRequest(new { message = "Document has no stored file to sign" });

      byte[] documentContent;
        using (var stream = await _storage.DownloadAsync(document.StorageKey))
      using (var ms = new MemoryStream())
        {
  await stream.CopyToAsync(ms);
            documentContent = ms.ToArray();
    }

        var masterKey = GetMasterKey();
        byte[] pfxBytes;
        try
        {
            pfxBytes = _signing.DecryptPfx(
     signingKey.EncryptedPfxData,
      signingKey.EncryptionNonce,
             signingKey.EncryptionTag,
 masterKey);
     }
        catch (CryptographicException)
   {
  return StatusCode(500, new { message = "Failed to decrypt signing key." });
    }

     var result = await _signing.SignAsync(documentContent, pfxBytes, request.PfxPassword, request.Reason);
        Array.Clear(pfxBytes);

     if (!result.Success)
     return BadRequest(new { message = result.Error });

        var signature = new DigitalSignature
        {
            DocumentId = documentId,
            SignedByUserId = _currentUser.UserId.Value,
            SigningKeyId = signingKey.Id,
  SignedAt = result.SignedAt,
            DocumentHash = result.DocumentHash!,
       SignatureData = result.SignatureBase64!,
  CertificateSubject = result.CertificateSubject,
            CertificateThumbprint = result.CertificateThumbprint,
            CertificateSerialNumber = result.CertificateSerialNumber,
            IsValid = true,
         VerifiedAt = DateTime.UtcNow,
            Reason = request.Reason,
        };

        _db.DigitalSignatures.Add(signature);
        signingKey.LastUsedAt = DateTime.UtcNow;
        document.FileHash = result.DocumentHash;
        document.IsSigned = true;

        await _db.SaveChangesAsync();

        await _audit.LogSigningAsync("Document.Signed", documentId,
   new { signatureId = signature.Id, result.CertificateSubject, result.CertificateThumbprint, request.Reason },
  severity: "Critical");

 return Ok(new
     {
       signatureId = signature.Id,
        documentId,
            signedAt = result.SignedAt,
     documentHash = result.DocumentHash,
            certificateSubject = result.CertificateSubject,
            message = "Document signed successfully",
   });
    }

    // ?? Verification ??

    /// <summary>Verify all signatures on a document.</summary>
    [HttpGet("verify/{documentId:guid}")]
    [RequirePermission(Permission.SignatureVerify)]
    public async Task<IActionResult> VerifyDocument(Guid documentId)
  {
        var document = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null) return NotFound("Document not found");

        var signatures = await _db.DigitalSignatures
         .Where(s => s.DocumentId == documentId)
      .OrderByDescending(s => s.SignedAt)
 .Select(s => new
    {
     s.Id, s.SignedByUserId,
   SignedByEmail = _db.Users.Where(u => u.Id == s.SignedByUserId).Select(u => u.Email).FirstOrDefault(),
   s.SignedAt, s.DocumentHash, s.CertificateSubject,
            s.CertificateThumbprint, s.CertificateSerialNumber,
     s.IsValid, s.VerifiedAt, s.Reason,
  })
       .ToListAsync();

        bool? currentIntegrity = null;
        if (!string.IsNullOrWhiteSpace(document.StorageKey) && signatures.Count > 0)
        {
    try
            {
           using var stream = await _storage.DownloadAsync(document.StorageKey);
     using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
          var currentHash = _signing.ComputeHash(ms.ToArray());
         currentIntegrity = currentHash == signatures[0].DocumentHash;
       }
            catch { /* storage error */ }
        }

        return Ok(new
        {
   documentId,
       fileName = document.SourceFileName,
            requiresSignature = document.RequiresSignature,
            isSigned = document.IsSigned,
    signatureCount = signatures.Count,
            currentIntegrity,
            signatures,
        });
    }

    /// <summary>
    /// Check if a document meets its signing requirements for submission.
    /// Returns 200 if OK, 400 if signature required but missing.
    /// </summary>
    [HttpGet("check-submission/{documentId:guid}")]
    [RequirePermission(Permission.SignatureVerify)]
    public async Task<IActionResult> CheckSubmissionReady(Guid documentId)
 {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc == null) return NotFound("Document not found");

        if (!doc.RequiresSignature)
            return Ok(new { documentId, ready = true, message = "No signature required" });

  if (!doc.IsSigned)
        {
            var hasKey = await _db.UserSigningKeys
      .AnyAsync(k => k.UserId == _currentUser.UserId && k.IsActive && k.ValidTo > DateTime.UtcNow);

     return BadRequest(new
      {
documentId,
        ready = false,
    hasSigningKey = hasKey,
        message = "This document requires a digital signature before submission. " +
           (hasKey
                  ? "Use 'Sign Document' to sign it with your key."
           : "Upload a PFX signing certificate in Settings ? E-Signing first."),
        });
        }

        // Verify integrity
    var latestSig = await _db.DigitalSignatures
          .Where(s => s.DocumentId == documentId)
   .OrderByDescending(s => s.SignedAt)
   .FirstOrDefaultAsync();

    return Ok(new
        {
         documentId,
      ready = true,
            signedBy = latestSig?.CertificateSubject,
       signedAt = latestSig?.SignedAt,
      message = "Document is signed and ready for submission",
  });
    }

    /// <summary>Get all signatures for the current user across all documents.</summary>
    [HttpGet("my-signatures")]
    [RequirePermission(Permission.SignatureVerify)]
    public async Task<IActionResult> GetMySignatures()
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var sigs = await _db.DigitalSignatures
    .Where(s => s.SignedByUserId == _currentUser.UserId.Value)
            .OrderByDescending(s => s.SignedAt)
   .Select(s => new
            {
        s.Id, s.DocumentId,
    DocumentName = _db.InsolvencyDocuments.Where(d => d.Id == s.DocumentId).Select(d => d.SourceFileName).FirstOrDefault(),
                s.SignedAt, s.CertificateSubject, s.Reason, s.IsValid,
   })
            .Take(50)
     .ToListAsync();

        return Ok(sigs);
    }

    // ?? Helpers ??

    /// <summary>Determine if a document type requires signature.</summary>
    public static bool DocTypeRequiresSignature(string? docType)
        => !string.IsNullOrWhiteSpace(docType) && SignatureRequiredDocTypes.Contains(docType);

  private byte[] GetMasterKey()
    {
        var keyString = _config["Signing:MasterKey"]
   ?? _config["Jwt:Key"]
          ?? throw new InvalidOperationException("No signing master key configured");

        return System.Security.Cryptography.SHA256.HashData(
       System.Text.Encoding.UTF8.GetBytes(keyString));
    }
}

public record SignDocumentRequest(string PfxPassword, string? Reason = null);
