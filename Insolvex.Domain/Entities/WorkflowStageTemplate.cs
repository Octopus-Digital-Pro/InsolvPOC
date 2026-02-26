namespace Insolvex.Domain.Entities;

/// <summary>
/// Links a <see cref="DocumentTemplate"/> to a <see cref="WorkflowStageDefinition"/>.
/// Defines which templates are required/optional outputs for a stage,
/// and in which order they appear in the stage's document checklist.
/// </summary>
public class WorkflowStageTemplate : BaseEntity
{
  public Guid StageDefinitionId { get; set; }
  public virtual WorkflowStageDefinition? StageDefinition { get; set; }

  public Guid DocumentTemplateId { get; set; }
  public virtual DocumentTemplate? DocumentTemplate { get; set; }

  /// <summary>Whether this template is mandatory to complete the stage.</summary>
  public bool IsRequired { get; set; }

  /// <summary>Display order within the stage.</summary>
  public int SortOrder { get; set; }

  /// <summary>Optional notes about when/how this template should be used.</summary>
  public string? Notes { get; set; }
}
