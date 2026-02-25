namespace Insolvex.Domain.Entities;

/// <summary>
/// Record of a digital signature applied to a document.
/// Stores the signature bytes, the signing certificate info,
/// and a hash of the document content at time of signing for verification.
/// </summary>
public class DigitalSignature : BaseEntity
{
   /// <summary>The document that was signed.</summary>
   public Guid DocumentId { get; set; }
   public virtual InsolvencyDocument? Document { get; set; }

   /// <summary>The user who signed.</summary>
   public Guid SignedByUserId { get; set; }
   public virtual User? SignedBy { get; set; }

   /// <summary>The signing key used (for audit trail even if key is later deleted).</summary>
   public Guid? SigningKeyId { get; set; }
   public virtual UserSigningKey? SigningKey { get; set; }

   /// <summary>When the signature was created.</summary>
   public DateTime SignedAt { get; set; }

   /// <summary>SHA-256 hash of the original document content at time of signing.</summary>
   public string DocumentHash { get; set; } = string.Empty;

   /// <summary>The PKCS#7/CMS detached signature bytes (Base64-encoded).</summary>
   public string SignatureData { get; set; } = string.Empty;

   /// <summary>Certificate subject name at time of signing.</summary>
   public string? CertificateSubject { get; set; }

   /// <summary>Certificate thumbprint at time of signing.</summary>
   public string? CertificateThumbprint { get; set; }

   /// <summary>Certificate serial number at time of signing.</summary>
   public string? CertificateSerialNumber { get; set; }

   /// <summary>Whether the signature has been verified as valid.</summary>
   public bool? IsValid { get; set; }

   /// <summary>Last verification timestamp.</summary>
   public DateTime? VerifiedAt { get; set; }

   /// <summary>Reason/purpose for signing (e.g., "Approval", "Final Report Sign-off").</summary>
   public string? Reason { get; set; }
}
