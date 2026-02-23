namespace Insolvex.Domain.Entities;

public class InsolvencyDocument : TenantScopedEntity
{
    public Guid CaseId { get; set; }
    public virtual InsolvencyCase? Case { get; set; }

    public string SourceFileName { get; set; } = string.Empty;
    public string DocType { get; set; } = "other";
    public string? DocumentDate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Full AI extraction result stored as JSON.
    /// </summary>
    public string? RawExtraction { get; set; }

    /// <summary>Storage key in IFileStorageService for the actual file.</summary>
    public string? StorageKey { get; set; }

    /// <summary>SHA-256 hash of file content for integrity verification.</summary>
    public string? FileHash { get; set; }

    /// <summary>Whether this document requires a digital signature before submission.</summary>
    public bool RequiresSignature { get; set; }

    /// <summary>Whether the signing requirement has been satisfied.</summary>
    public bool IsSigned { get; set; }

    /// <summary>Purpose of the document (Generated, Uploaded, ForSubmission, Internal).</summary>
    public string? Purpose { get; set; }

    // ?? Extraction fields per InsolvencyAppRules ??

    /// <summary>AI-generated document summary (editable by reviewer).</summary>
    public string? Summary { get; set; }

    /// <summary>JSON array of extracted parties [{name, role, identifier}].</summary>
    public string? PartiesExtractedJson { get; set; }

    /// <summary>JSON array of extracted dates [{date, meaning, source}].</summary>
    public string? DatesExtractedJson { get; set; }

    /// <summary>JSON array of extracted actions/obligations [{action, deadline, assignee}].</summary>
    public string? ActionsExtractedJson { get; set; }

    /// <summary>JSON map of structured fields to populate case/party/claim entities.</summary>
    public string? FieldsExtractedJson { get; set; }

    /// <summary>Confidence score 0-100 for the doc type classification.</summary>
    public int? ClassificationConfidence { get; set; }

    /// <summary>User who reviewed the extracted data.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>When the extracted data was reviewed.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>If the reviewer overrode the doc type, the original type.</summary>
    public string? OverrideType { get; set; }

    /// <summary>Document version counter.</summary>
    public int Version { get; set; } = 1;

    // Navigation
    public ICollection<DigitalSignature> Signatures { get; set; } = new List<DigitalSignature>();
}
