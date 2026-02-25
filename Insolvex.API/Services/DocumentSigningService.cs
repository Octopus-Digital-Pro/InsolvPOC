using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Insolvex.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Insolvex.API.Services;

/// <summary>
/// Document signing service using .NET's built-in CMS/PKCS#7 and X.509 support.
/// Signs documents with detached CMS signatures using PKCS#12 (PFX) certificates.
/// PFX storage is encrypted with AES-256-GCM.
/// </summary>
public class DocumentSigningService : IDocumentSigningService
{
  private readonly ILogger<DocumentSigningService> _logger;

  public DocumentSigningService(ILogger<DocumentSigningService> logger)
  {
    _logger = logger;
  }

  public Task<SigningResult> SignAsync(byte[] documentContent, byte[] pfxBytes, string pfxPassword, string? reason = null)
  {
    try
    {
      using var cert = new X509Certificate2(pfxBytes, pfxPassword,
X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

      if (cert.NotAfter < DateTime.UtcNow)
      {
        return Task.FromResult(new SigningResult
        {
          Success = false,
          Error = $"Certificate expired on {cert.NotAfter:yyyy-MM-dd}"
        });
      }

      var contentInfo = new ContentInfo(documentContent);
      var signedCms = new SignedCms(contentInfo, detached: true);
      var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert)
      {
        DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1") // SHA-256
      };

      signedCms.ComputeSignature(signer);
      var signatureBytes = signedCms.Encode();

      return Task.FromResult(new SigningResult
      {
        Success = true,
        SignatureBase64 = Convert.ToBase64String(signatureBytes),
        DocumentHash = ComputeHash(documentContent),
        CertificateSubject = cert.Subject,
        CertificateThumbprint = cert.Thumbprint,
        CertificateSerialNumber = cert.SerialNumber,
        SignedAt = DateTime.UtcNow,
      });
    }
    catch (CryptographicException ex)
    {
      _logger.LogError(ex, "Signing failed: cryptographic error");
      return Task.FromResult(new SigningResult
      {
        Success = false,
        Error = $"Signing failed: {ex.Message}"
      });
    }
  }

  public Task<VerificationResult> VerifyAsync(byte[] documentContent, string signatureBase64)
  {
    try
    {
      var signatureBytes = Convert.FromBase64String(signatureBase64);
      var contentInfo = new ContentInfo(documentContent);
      var signedCms = new SignedCms(contentInfo, detached: true);
      signedCms.Decode(signatureBytes);

      // CheckSignature throws if invalid
      signedCms.CheckSignature(verifySignatureOnly: true);

      var signerInfo = signedCms.SignerInfos[0];
      var cert = signerInfo.Certificate;

      return Task.FromResult(new VerificationResult
      {
        IsValid = true,
        CertificateSubject = cert?.Subject,
        CertificateThumbprint = cert?.Thumbprint,
        SignedAt = cert?.NotBefore,
      });
    }
    catch (CryptographicException ex)
    {
      return Task.FromResult(new VerificationResult
      {
        IsValid = false,
        Error = $"Verification failed: {ex.Message}"
      });
    }
  }

  public CertificateInfo ExtractCertificateInfo(byte[] pfxBytes, string pfxPassword)
  {
    using var cert = new X509Certificate2(pfxBytes, pfxPassword,
  X509KeyStorageFlags.EphemeralKeySet);

    return new CertificateInfo
    {
      SubjectName = cert.Subject,
      IssuerName = cert.Issuer,
      SerialNumber = cert.SerialNumber,
      Thumbprint = cert.Thumbprint,
      ValidFrom = cert.NotBefore,
      ValidTo = cert.NotAfter,
    };
  }

  public string ComputeHash(byte[] content)
  {
    var hash = SHA256.HashData(content);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }

  public EncryptedData EncryptPfx(byte[] pfxBytes, byte[] masterKey)
  {
    var nonce = new byte[12]; // AES-GCM standard nonce size
    RandomNumberGenerator.Fill(nonce);

    var cipherText = new byte[pfxBytes.Length];
    var tag = new byte[16]; // AES-GCM standard tag size

    using var aesGcm = new AesGcm(masterKey, tagSizeInBytes: 16);
    aesGcm.Encrypt(nonce, pfxBytes, cipherText, tag);

    return new EncryptedData
    {
      CipherText = cipherText,
      Nonce = nonce,
      Tag = tag,
    };
  }

  public byte[] DecryptPfx(byte[] encryptedData, byte[] nonce, byte[] tag, byte[] masterKey)
  {
    var plainText = new byte[encryptedData.Length];
    using var aesGcm = new AesGcm(masterKey, tagSizeInBytes: 16);
    aesGcm.Decrypt(nonce, encryptedData, tag, plainText);
    return plainText;
  }
}
