namespace Insolvex.Domain.Entities;

/// <summary>
/// A user's digital signing key (PKCS#12 / PFX certificate).
/// The private key material is stored encrypted.
/// Each user may have one active signing key at a time.
/// </summary>
public class UserSigningKey : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User? User { get; set; }

    /// <summary>Friendly name for the certificate (e.g., "My Signing Key 2025").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Certificate subject (CN) extracted from the PFX.</summary>
    public string? SubjectName { get; set; }

    /// <summary>Certificate issuer extracted from the PFX.</summary>
    public string? IssuerName { get; set; }

    /// <summary>Certificate serial number (hex).</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Certificate thumbprint (SHA-1 hash, hex).</summary>
    public string? Thumbprint { get; set; }

    /// <summary>Certificate valid-from date.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Certificate valid-to / expiry date.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// The PFX/PKCS#12 bytes encrypted with AES-256-GCM using the application's
 /// data-protection key. Never stored in plaintext.
    /// </summary>
    public byte[] EncryptedPfxData { get; set; } = Array.Empty<byte>();

    /// <summary>AES nonce used for the encryption.</summary>
    public byte[] EncryptionNonce { get; set; } = Array.Empty<byte>();

    /// <summary>AES authentication tag.</summary>
    public byte[] EncryptionTag { get; set; } = Array.Empty<byte>();

    /// <summary>Whether this is the user's currently active signing key.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When this key was last used to sign a document.</summary>
    public DateTime? LastUsedAt { get; set; }
}
