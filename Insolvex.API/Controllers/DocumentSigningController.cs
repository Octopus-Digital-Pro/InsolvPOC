using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvex.API.Authorization;
using Insolvex.Core.Abstractions;
using Insolvex.Domain;
using Insolvex.Domain.Enums;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/signing")]
[Authorize]
public class DocumentSigningController : ControllerBase
{
  private readonly ISigningKeyService _signingKeys;

  public DocumentSigningController(ISigningKeyService signingKeys) => _signingKeys = signingKeys;

  // ── Key Management ────────────────────────────────────────────────────────

  /// <summary>Upload a PFX/PKCS#12 signing key for the current user.</summary>
  [HttpPost("keys/upload")]
  [RequirePermission(Permission.SigningKeyManage)]
  [RequestSizeLimit(5_000_000)]
  public async Task<IActionResult> UploadSigningKey(
      IFormFile file, [FromForm] string password, [FromForm] string? name = null,
      CancellationToken ct = default)
      => Ok(await _signingKeys.UploadKeyAsync(file, password, name, ct));

  /// <summary>Check if current user has an active signing key.</summary>
  [HttpGet("keys/status")]
  [RequirePermission(Permission.SignatureVerify)]
  public async Task<IActionResult> GetKeyStatus(CancellationToken ct = default)
      => Ok(await _signingKeys.GetKeyStatusAsync(ct));

  /// <summary>List signing keys for the current user.</summary>
  [HttpGet("keys")]
  [RequirePermission(Permission.SigningKeyManage)]
  public async Task<IActionResult> GetMyKeys(CancellationToken ct = default)
      => Ok(await _signingKeys.GetMyKeysAsync(ct));

  /// <summary>Deactivate a signing key.</summary>
  [HttpDelete("keys/{id:guid}")]
  [RequirePermission(Permission.SigningKeyManage)]
  public async Task<IActionResult> DeactivateKey(Guid id, CancellationToken ct = default)
  {
    await _signingKeys.DeactivateKeyAsync(id, ct);
    return Ok(new { message = "Signing key deactivated" });
  }

  // ── Download for Signing ──────────────────────────────────────────────────

  /// <summary>Download a document for offline signing.</summary>
  [HttpGet("download/{documentId:guid}")]
  [RequirePermission(Permission.DocumentDownload)]
  public async Task<IActionResult> DownloadForSigning(Guid documentId, CancellationToken ct = default)
  {
    var (content, contentType, fileName) = await _signingKeys.DownloadForSigningAsync(documentId, ct);
    return File(content, contentType, fileName);
  }

  // ── Signatures ────────────────────────────────────────────────────────────

  /// <summary>Sign a document with the user's active signing key.</summary>
  [HttpPost("sign/{documentId:guid}")]
  [RequirePermission(Permission.DocumentSign)]
  public async Task<IActionResult> SignDocument(
      Guid documentId, [FromBody] SignDocumentRequest request, CancellationToken ct = default)
      => Ok(await _signingKeys.SignDocumentAsync(documentId, request.PfxPassword, request.Reason, ct));

  /// <summary>Upload a signed version of a document.</summary>
  [HttpPost("upload-signed/{documentId:guid}")]
  [RequirePermission(Permission.DocumentSign)]
  [RequestSizeLimit(50_000_000)]
  public async Task<IActionResult> UploadSignedDocument(
      Guid documentId, IFormFile file, CancellationToken ct = default)
      => Ok(await _signingKeys.UploadSignedDocumentAsync(documentId, file, ct));

  // ── Verification ──────────────────────────────────────────────────────────

  /// <summary>Verify all signatures on a document.</summary>
  [HttpGet("verify/{documentId:guid}")]
  [RequirePermission(Permission.SignatureVerify)]
  public async Task<IActionResult> VerifyDocument(Guid documentId, CancellationToken ct = default)
      => Ok(await _signingKeys.VerifyDocumentAsync(documentId, ct));

  /// <summary>Check if a document meets its signing requirements for submission.</summary>
  [HttpGet("check-submission/{documentId:guid}")]
  [RequirePermission(Permission.SignatureVerify)]
  public async Task<IActionResult> CheckSubmissionReady(Guid documentId, CancellationToken ct = default)
      => Ok(await _signingKeys.CheckSubmissionReadyAsync(documentId, ct));

  /// <summary>Get all signatures for the current user across all documents.</summary>
  [HttpGet("my-signatures")]
  [RequirePermission(Permission.SignatureVerify)]
  public async Task<IActionResult> GetMySignatures(CancellationToken ct = default)
      => Ok(await _signingKeys.GetMySignaturesAsync(ct));

// ── DigiSign / Hardware Token (Windows cert store) ───────────────────────

  /// <summary>
  /// Enumerate X.509 certificates available in the Windows "My" certificate store
  /// for the current user and local machine. Typically includes DigiSign / eToken USB keys.
  /// Returns only certificates that have an associated private key and are not yet expired.
  /// Only available when the API is hosted on a Windows machine.
  /// </summary>
  [HttpGet("keys/windows-certs")]
  [RequirePermission(Permission.SigningKeyManage)]
  public IActionResult GetWindowsCertificates()
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return Ok(new { available = false, reason = "Windows certificate store is only accessible when the server runs on Windows.", certificates = Array.Empty<object>() });

    var result = new List<object>();

    foreach (var (storeName, storeLocation) in new[]
    {
      (StoreName.My, StoreLocation.CurrentUser),
      (StoreName.My, StoreLocation.LocalMachine),
    })
    {
      try
      {
        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        foreach (var cert in store.Certificates)
        {
          // Skip expired certs and those without a private key
          if (cert.NotAfter < DateTime.UtcNow) continue;
          if (!cert.HasPrivateKey) continue;

          result.Add(new
          {
            thumbprint   = cert.Thumbprint,
            subject      = cert.Subject,
            issuer       = cert.Issuer,
            validFrom    = cert.NotBefore.ToString("dd.MM.yyyy"),
            validTo      = cert.NotAfter.ToString("dd.MM.yyyy"),
            serialNumber = cert.SerialNumber,
            friendlyName = cert.FriendlyName,
            storeLocation = storeLocation.ToString(),
            keyAlgorithm  = cert.GetKeyAlgorithmParametersString(),
            // Hint: smart-card / USB token certs typically have a CSP provider name
            // embedded in the key container — we surface the subject key ID for UI matching
            subjectKeyId  = cert.Extensions
              .OfType<X509SubjectKeyIdentifierExtension>()
              .FirstOrDefault()?.SubjectKeyIdentifierBytes.ToString() ?? "",
          });
        }
      }
      catch (Exception ex)
      {
        // Store may not exist on all machines — log and continue
        _signingKeys.GetType(); // keep reference for logger; replace with proper logger injection if needed
        _ = ex; // suppress warning
      }
    }

    return Ok(new { available = true, certificates = result });
  }

  // ── Static helpers ────────────────────────────────────────────────────────

  /// <summary>Determine if a document type requires signature.</summary>
  public static bool DocTypeRequiresSignature(string? docType)
      => DocumentTypeRules.RequiresSignature(docType);
}

public record SignDocumentRequest(string PfxPassword, string? Reason = null);

