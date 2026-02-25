namespace Insolvex.Core.Configuration;

/// <summary>
/// Configuration for AWS S3 file storage.
/// </summary>
public class S3StorageOptions
{
  public const string SectionName = "Aws:S3";

  /// <summary>AWS access key ID.</summary>
  public string AccessKeyId { get; set; } = string.Empty;

  /// <summary>AWS secret access key.</summary>
  public string SecretAccessKey { get; set; } = string.Empty;

  /// <summary>AWS region (e.g. "eu-central-1").</summary>
  public string Region { get; set; } = "eu-central-1";

  /// <summary>S3 bucket name.</summary>
  public string BucketName { get; set; } = string.Empty;

  /// <summary>Optional key prefix (folder) for all files.</summary>
  public string KeyPrefix { get; set; } = "documents/";

  /// <summary>Optional custom endpoint URL (for MinIO, LocalStack, etc.).</summary>
  public string? ServiceUrl { get; set; }

  /// <summary>Whether to force path-style addressing (for MinIO / LocalStack).</summary>
  public bool ForcePathStyle { get; set; }
}
