namespace Insolvio.Core.Abstractions;

/// <summary>
/// Abstraction for digital document signing operations.
/// </summary>
public interface IDocumentSigningService
{
    /// <summary>
    /// Sign document content using a PFX certificate.
    /// Returns a detached CMS/PKCS#7 signature (Base64).
    /// </summary>
    Task<SigningResult> SignAsync(byte[] documentContent, byte[] pfxBytes, string pfxPassword, string? reason = null);

    /// <summary>
    /// Verify a detached CMS signature against document content.
    /// </summary>
    Task<VerificationResult> VerifyAsync(byte[] documentContent, string signatureBase64);

    /// <summary>
    /// Extract certificate metadata from PFX bytes without the password being stored.
    /// </summary>
    CertificateInfo ExtractCertificateInfo(byte[] pfxBytes, string pfxPassword);

    /// <summary>
    /// Compute SHA-256 hash of content, returned as lowercase hex string.
    /// </summary>
    string ComputeHash(byte[] content);

    /// <summary>
    /// Encrypt PFX bytes using AES-256-GCM for secure storage.
    /// </summary>
    EncryptedData EncryptPfx(byte[] pfxBytes, byte[] masterKey);

    /// <summary>
    /// Decrypt PFX bytes from AES-256-GCM encrypted storage.
    /// </summary>
    byte[] DecryptPfx(byte[] encryptedData, byte[] nonce, byte[] tag, byte[] masterKey);
}

public class SigningResult
{
    public bool Success { get; set; }
    public string? SignatureBase64 { get; set; }
    public string? DocumentHash { get; set; }
    public string? CertificateSubject { get; set; }
    public string? CertificateThumbprint { get; set; }
    public string? CertificateSerialNumber { get; set; }
    public DateTime SignedAt { get; set; }
    public string? Error { get; set; }
}

public class VerificationResult
{
    public bool IsValid { get; set; }
    public string? CertificateSubject { get; set; }
    public string? CertificateThumbprint { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? Error { get; set; }
}

public class CertificateInfo
{
    public string SubjectName { get; set; } = string.Empty;
    public string IssuerName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsExpired => DateTime.UtcNow > ValidTo;
}

public class EncryptedData
{
    public byte[] CipherText { get; set; } = Array.Empty<byte>();
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] Tag { get; set; } = Array.Empty<byte>();
}
