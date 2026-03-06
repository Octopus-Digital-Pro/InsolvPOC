using Insolvio.Core.DTOs;
using Microsoft.AspNetCore.Http;

namespace Insolvio.Core.Abstractions;

/// <summary>
/// Service for digital signing key management and document signature persistence.
/// Cryptographic operations (sign/verify/encrypt) remain in IDocumentSigningService.
/// </summary>
public interface ISigningKeyService
{
    // User-level signing preferences
    Task<SigningPreferenceDto> GetMyPreferenceAsync(CancellationToken ct = default);
    Task<SigningPreferenceDto> UpdateMyPreferenceAsync(bool useSavedSigningKey, CancellationToken ct = default);

    // Key management
    Task<SigningKeyDto> UploadKeyAsync(IFormFile file, string password, string? name, CancellationToken ct = default);
    Task<KeyStatusDto> GetKeyStatusAsync(CancellationToken ct = default);
    Task<List<SigningKeyDto>> GetMyKeysAsync(CancellationToken ct = default);
    Task DeactivateKeyAsync(Guid keyId, CancellationToken ct = default);

    // Document download
    Task<(Stream Content, string ContentType, string FileName)> DownloadForSigningAsync(Guid documentId, CancellationToken ct = default);

    // Signatures
    Task<SignatureDto> SignDocumentAsync(Guid documentId, string pfxPassword, string? reason, CancellationToken ct = default);
    Task<SignatureDto> UploadSignedDocumentAsync(Guid documentId, IFormFile file, CancellationToken ct = default);
    Task<object> VerifyDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<object> CheckSubmissionReadyAsync(Guid documentId, CancellationToken ct = default);
    Task<List<SignatureDto>> GetMySignaturesAsync(CancellationToken ct = default);
}
