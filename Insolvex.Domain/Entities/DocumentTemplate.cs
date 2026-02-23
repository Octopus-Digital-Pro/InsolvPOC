using Insolvex.Domain.Enums;

namespace Insolvex.Domain.Entities;

/// <summary>
/// A mail-merge document template that can be uploaded via Settings.
/// Templates are global by default (TenantId = null) but can be overridden
/// per-tenant by uploading a tenant-specific version with the same TemplateType.
/// Resolution order: tenant-specific ? global fallback.
/// </summary>
public class DocumentTemplate : BaseEntity
{
    /// <summary>Optional tenant scope. Null = global template available to all tenants.</summary>
    public Guid? TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }

  /// <summary>The logical template type this file implements.</summary>
  public DocumentTemplateType TemplateType { get; set; }

    /// <summary>Human-readable display name.</summary>
 public string Name { get; set; } = string.Empty;

  /// <summary>Original filename as uploaded.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Storage key in IFileStorageService (local path or S3 key).</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>MIME content type (application/msword, application/pdf, etc.).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 hash of the file content for integrity.</summary>
    public string? FileHash { get; set; }

 /// <summary>Optional description or notes about this template.</summary>
    public string? Description { get; set; }

    /// <summary>The workflow stage this template is typically used in.</summary>
    public string? Stage { get; set; }

    /// <summary>JSON schema describing required merge fields (for validation).</summary>
    public string? MergeFieldsJson { get; set; }

    /// <summary>Whether this template is active and selectable.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Version counter for audit trail.</summary>
    public int Version { get; set; } = 1;
}
