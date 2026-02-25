namespace Insolvex.Core.Abstractions;

/// <summary>
/// Abstraction for file storage (S3, Azure Blob, local disk, etc.).
/// </summary>
public interface IFileStorageService
{
  /// <summary>
  /// Upload a file and return its storage key (path).
  /// </summary>
  Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

  /// <summary>
  /// Download a file by its storage key.
  /// </summary>
  Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

  /// <summary>
  /// Delete a file by its storage key.
  /// </summary>
  Task DeleteAsync(string key, CancellationToken ct = default);

  /// <summary>
  /// Check if a file exists.
  /// </summary>
  Task<bool> ExistsAsync(string key, CancellationToken ct = default);

  /// <summary>
  /// Generate a pre-signed URL for temporary access (e.g., for downloads).
  /// </summary>
  string GetPresignedUrl(string key, TimeSpan expiry);
}
