namespace Insolvio.Domain.Enums;

/// <summary>
/// The type of file storage provider to use for document storage.
/// Selectable globally under Settings by a GlobalAdmin.
/// </summary>
public enum StorageProviderType
{
    /// <summary>Local disk storage (default for development)</summary>
    Local,

    /// <summary>Amazon S3 (or S3-compatible such as MinIO)</summary>
    AwsS3,
}
