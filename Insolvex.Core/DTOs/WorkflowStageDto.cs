namespace Insolvex.Core.DTOs;

/// <summary>DTO for workflow stage definitions (list view).</summary>
public record WorkflowStageDto(
    Guid Id,
    Guid? TenantId,
    string StageKey,
    string Name,
    string? Description,
    int SortOrder,
    string? ApplicableProcedureTypes,
    bool IsActive,
    int TemplateCount,
    DateTime CreatedOn,
    DateTime? LastModifiedOn);

/// <summary>DTO for workflow stage definitions (detail view with JSON configs).</summary>
public record WorkflowStageDetailDto(
    Guid Id,
    Guid? TenantId,
    string StageKey,
    string Name,
    string? Description,
    int SortOrder,
    string? ApplicableProcedureTypes,
    string? RequiredFieldsJson,
    string? RequiredPartyRolesJson,
    string? RequiredDocTypesJson,
    string? RequiredTaskTemplatesJson,
    string? ValidationRulesJson,
    string? OutputDocTypesJson,
    string? OutputTasksJson,
    string? AllowedTransitionsJson,
    bool IsActive,
    List<WorkflowStageTemplateDto> Templates,
    DateTime CreatedOn,
    DateTime? LastModifiedOn);

/// <summary>DTO for a template linked to a workflow stage.</summary>
public record WorkflowStageTemplateDto(
    Guid Id,
    Guid DocumentTemplateId,
    string TemplateName,
    string? TemplateType,
    bool IsRequired,
    int SortOrder,
    string? Notes);

/// <summary>Command to create or update a workflow stage definition.</summary>
public class UpsertWorkflowStageCommand
{
  public string StageKey { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public int SortOrder { get; set; }
  public string? ApplicableProcedureTypes { get; set; }
  public string? RequiredFieldsJson { get; set; }
  public string? RequiredPartyRolesJson { get; set; }
  public string? RequiredDocTypesJson { get; set; }
  public string? RequiredTaskTemplatesJson { get; set; }
  public string? ValidationRulesJson { get; set; }
  public string? OutputDocTypesJson { get; set; }
  public string? OutputTasksJson { get; set; }
  public string? AllowedTransitionsJson { get; set; }
  public bool IsActive { get; set; } = true;
  public List<UpsertStageTemplateItem>? Templates { get; set; }
}

/// <summary>Item in the templates array when upserting a workflow stage.</summary>
public class UpsertStageTemplateItem
{
  public Guid DocumentTemplateId { get; set; }
  public bool IsRequired { get; set; }
  public int SortOrder { get; set; }
  public string? Notes { get; set; }
}
