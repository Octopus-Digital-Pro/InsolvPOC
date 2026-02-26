using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Insolvex.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Exceptions;
using Insolvex.Core.Mapping;
using Insolvex.Domain;
using Insolvex.Domain.Entities;

namespace Insolvex.Data.Services;

public sealed class SigningKeyService : ISigningKeyService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentSigningService _signing;
    private readonly IFileStorageService _storage;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public SigningKeyService(
        ApplicationDbContext db, ICurrentUserService currentUser,
        IDocumentSigningService signing, IFileStorageService storage,
        IConfiguration config, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _signing = signing;
        _storage = storage;
        _config = config;
        _audit = audit;
    }

    public async Task<SigningPreferenceDto> GetMyPreferenceAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct)
            ?? throw new NotFoundException("User", _currentUser.UserId.Value);

        return new SigningPreferenceDto(user.UseSavedSigningKey);
    }

    public async Task<SigningPreferenceDto> UpdateMyPreferenceAsync(bool useSavedSigningKey, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct)
            ?? throw new NotFoundException("User", _currentUser.UserId.Value);

        user.UseSavedSigningKey = useSavedSigningKey;
        await _db.SaveChangesAsync(ct);

        await _audit.LogSigningAsync("Signing Preference Updated", user.Id,
            new { useSavedSigningKey });

        return new SigningPreferenceDto(user.UseSavedSigningKey);
    }

    public async Task<SigningKeyDto> UploadKeyAsync(IFormFile file, string password, string? name, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pfx" && ext != ".p12")
            throw new BusinessException("Only .pfx or .p12 files are accepted");

        byte[] pfxBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            pfxBytes = ms.ToArray();
        }

        CertificateInfo certInfo;
        try
        {
            certInfo = _signing.ExtractCertificateInfo(pfxBytes, password);
        }
        catch (CryptographicException)
        {
            throw new BusinessException("Invalid PFX file or incorrect password");
        }

        if (certInfo.IsExpired)
            throw new BusinessException($"Certificate expired on {certInfo.ValidTo:yyyy-MM-dd}");

        var masterKey = GetMasterKey();
        var encrypted = _signing.EncryptPfx(pfxBytes, masterKey);
        Array.Clear(pfxBytes);

        // Deactivate existing active keys
        var existingKeys = await _db.UserSigningKeys
            .Where(k => k.UserId == _currentUser.UserId.Value && k.IsActive)
            .ToListAsync(ct);
        foreach (var k in existingKeys) k.IsActive = false;

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
        await _db.SaveChangesAsync(ct);
        await _audit.LogSigningAsync("Digital Signing Key Uploaded", signingKey.Id,
            new { signingKey.Name, certInfo.SubjectName, certInfo.Thumbprint, certInfo.ValidFrom, certInfo.ValidTo });

        return signingKey.ToDto();
    }

    public async Task<KeyStatusDto> GetKeyStatusAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");
        var key = await _db.UserSigningKeys
            .Where(k => k.UserId == _currentUser.UserId.Value && k.IsActive)
            .FirstOrDefaultAsync(ct);
        if (key == null) return new KeyStatusDto(false, false, null);
        var dto = key.ToDto();
        return new KeyStatusDto(true, !dto.IsExpired, dto);
    }

    public async Task<List<SigningKeyDto>> GetMyKeysAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");
        return await _db.UserSigningKeys
            .Where(k => k.UserId == _currentUser.UserId.Value)
            .OrderByDescending(k => k.IsActive)
            .ThenByDescending(k => k.CreatedOn)
            .Select(k => k.ToDto())
            .ToListAsync(ct);
    }

    public async Task DeactivateKeyAsync(Guid keyId, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");
        var key = await _db.UserSigningKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == _currentUser.UserId.Value, ct)
            ?? throw new NotFoundException("UserSigningKey", keyId);
        key.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogSigningAsync("Digital Signing Key Deactivated", keyId, new { key.Name, key.Thumbprint });
    }

    public async Task<SignatureDto> SignDocumentAsync(Guid documentId, string pfxPassword, string? reason, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");

        var signingKey = await _db.UserSigningKeys
            .FirstOrDefaultAsync(k => k.UserId == _currentUser.UserId.Value && k.IsActive, ct)
            ?? throw new BusinessException("No active signing key. Upload a PFX certificate first.");
        if (signingKey.ValidTo < DateTime.UtcNow)
            throw new BusinessException("Your signing certificate has expired");

        var document = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("InsolvencyDocument", documentId);
        if (string.IsNullOrWhiteSpace(document.StorageKey))
            throw new BusinessException("Document has no stored file to sign");

        byte[] documentContent;
        using (var stream = await _storage.DownloadAsync(document.StorageKey))
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms, ct);
            documentContent = ms.ToArray();
        }

        var masterKey = GetMasterKey();
        byte[] pfxBytes;
        try
        {
            pfxBytes = _signing.DecryptPfx(signingKey.EncryptedPfxData, signingKey.EncryptionNonce, signingKey.EncryptionTag, masterKey);
        }
        catch (CryptographicException)
        {
            throw new BusinessException("Failed to decrypt signing key.");
        }

        var result = await _signing.SignAsync(documentContent, pfxBytes, pfxPassword, reason);
        Array.Clear(pfxBytes);

        if (!result.Success)
            throw new BusinessException(result.Error ?? "Signing failed");

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
            Reason = reason,
        };

        _db.DigitalSignatures.Add(signature);
        signingKey.LastUsedAt = DateTime.UtcNow;
        document.FileHash = result.DocumentHash;
        document.IsSigned = true;

        await _db.SaveChangesAsync(ct);
        await _audit.LogSigningAsync("Document Digitally Signed", documentId,
            new { signatureId = signature.Id, result.CertificateSubject, result.CertificateThumbprint, reason },
            severity: "Critical");

        return ToSignatureDto(signature, null);
    }

    public async Task<SignatureDto> UploadSignedDocumentAsync(Guid documentId, IFormFile file, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");

        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("InsolvencyDocument", documentId);

        byte[] fileContent;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            fileContent = ms.ToArray();
        }

        var fileHash = _signing.ComputeHash(fileContent);
        var signedKey = $"{doc.CaseId}/signed/{file.FileName}";
        using var uploadStream = new MemoryStream(fileContent);
        await _storage.UploadAsync(signedKey, uploadStream, file.ContentType);

        doc.StorageKey = signedKey;
        doc.FileHash = fileHash;
        doc.SourceFileName = file.FileName;
        doc.IsSigned = true;

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
        await _db.SaveChangesAsync(ct);
        await _audit.LogSigningAsync("Signed Document Uploaded", documentId,
            new { fileName = file.FileName, fileHash, signatureId = signature.Id });

        return ToSignatureDto(signature, null);
    }

    public async Task<object> VerifyDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("InsolvencyDocument", documentId);

        var signatures = await _db.DigitalSignatures
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.SignedAt)
            .Select(s => new
            {
                s.Id,
                s.SignedByUserId,
                SignedByEmail = _db.Users.Where(u => u.Id == s.SignedByUserId).Select(u => u.Email).FirstOrDefault(),
                s.SignedAt,
                s.DocumentHash,
                s.CertificateSubject,
                s.CertificateThumbprint,
                s.CertificateSerialNumber,
                s.IsValid,
                s.VerifiedAt,
                s.Reason,
            })
            .ToListAsync(ct);

        bool? currentIntegrity = null;
        if (!string.IsNullOrWhiteSpace(document.StorageKey) && signatures.Count > 0)
        {
            try
            {
                using var stream = await _storage.DownloadAsync(document.StorageKey);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var currentHash = _signing.ComputeHash(ms.ToArray());
                currentIntegrity = currentHash == signatures[0].DocumentHash;
            }
            catch { /* storage error */ }
        }

        return new
        {
            documentId,
            fileName = document.SourceFileName,
            requiresSignature = document.RequiresSignature,
            isSigned = document.IsSigned,
            signatureCount = signatures.Count,
            currentIntegrity,
            signatures,
        };
    }

    public async Task<object> CheckSubmissionReadyAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("InsolvencyDocument", documentId);

        if (!doc.RequiresSignature)
            return new { documentId, ready = true, message = "No signature required" };

        if (!doc.IsSigned)
        {
            var hasKey = await _db.UserSigningKeys
                .AnyAsync(k => k.UserId == _currentUser.UserId && k.IsActive && k.ValidTo > DateTime.UtcNow, ct);
            return new
            {
                documentId,
                ready = false,
                hasSigningKey = hasKey,
                message = "This document requires a digital signature before submission. " +
                          (hasKey ? "Use 'Sign Document' to sign it with your key."
                                  : "Upload a PFX signing certificate in Settings → E-Signing first."),
            };
        }

        var latestSig = await _db.DigitalSignatures
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.SignedAt)
            .FirstOrDefaultAsync(ct);

        return new
        {
            documentId,
            ready = true,
            signedBy = latestSig?.CertificateSubject,
            signedAt = latestSig?.SignedAt,
            message = "Document is signed and ready for submission",
        };
    }

    public async Task<(Stream Content, string ContentType, string FileName)> DownloadForSigningAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _db.InsolvencyDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("InsolvencyDocument", documentId);
        if (string.IsNullOrWhiteSpace(doc.StorageKey))
            throw new BusinessException("Document has no stored file");
        var stream = await _storage.DownloadAsync(doc.StorageKey);
        var contentType = doc.SourceFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf" : "application/octet-stream";
        return (stream, contentType, doc.SourceFileName);
    }

    public async Task<List<SignatureDto>> GetMySignaturesAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) throw new BusinessException("User not authenticated");
        var sigs = await _db.DigitalSignatures
            .Where(s => s.SignedByUserId == _currentUser.UserId.Value)
            .OrderByDescending(s => s.SignedAt)
            .Take(50)
            .ToListAsync(ct);

        var userEmail = await _db.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        return sigs.Select(s =>
        {
            var docName = _db.InsolvencyDocuments
                .Where(d => d.Id == s.DocumentId)
                .Select(d => d.SourceFileName)
                .FirstOrDefault();
            return ToSignatureDto(s, docName, userEmail);
        }).ToList();
    }

    private byte[] GetMasterKey()
    {
        var keyString = _config["Signing:MasterKey"]
            ?? _config["Jwt:Key"]
            ?? throw new InvalidOperationException("No signing master key configured");
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(keyString));
    }

    private static SignatureDto ToSignatureDto(DigitalSignature s, string? docName, string? signedByEmail = null) => new(
        s.Id, s.DocumentId, docName, s.SignedByUserId, signedByEmail,
        s.SignedAt, s.DocumentHash, s.CertificateSubject, s.CertificateThumbprint,
        s.CertificateSerialNumber, s.IsValid, s.VerifiedAt, s.Reason);
}
