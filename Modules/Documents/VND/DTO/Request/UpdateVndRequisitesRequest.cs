using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateVndRequisitesRequest : IValidatableObject
{
    public required int TypeId { get; set; }
    public required int OrganId { get; set; }

    public int? DeveloperId { get; set; }
    public int? CuratorDeveloperId { get; set; }
    public List<int> ResponsibleExecutorIds { get; set; } = [];

    [Required(ErrorMessage = "Название ВНД на русском является обязательным полем!")]
    [StringLength(500, MinimumLength = 3)]
    public required string TitleRu { get; set; }

    [StringLength(500)]
    public string? TitleEn { get; set; }

    [StringLength(500)]
    public string? TitleKg { get; set; }

    public DateOnly? AdoptionDate { get; set; }

    [StringLength(100)]
    public string? AdoptionCode { get; set; }

    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? DueActualizationDate { get; set; }
    public DateOnly? LastActualizationDate { get; set; }
    public bool LastActualizationHadChanges { get; set; }

    public DateOnly? CancelDate { get; set; }

    [StringLength(100)]
    public string? CancelCode { get; set; }

    [StringLength(2000)]
    public string? CancelReason { get; set; }

    public DateOnly? ArchivedDate { get; set; }

    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public int? SecrecyLevelId { get; set; }
    public List<int> UserGroupIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CancelDate.HasValue && AdoptionDate.HasValue && CancelDate.Value < AdoptionDate.Value)
            yield return new ValidationResult(
                "Дата отмены не может быть раньше даты принятия",
                [nameof(CancelDate)]);

        if (ArchivedDate.HasValue && CancelDate.HasValue && ArchivedDate.Value < CancelDate.Value)
            yield return new ValidationResult(
                "Дата архивации не может быть раньше даты отмены",
                [nameof(ArchivedDate)]);

        if (EffectiveDate.HasValue && AdoptionDate.HasValue && EffectiveDate.Value < AdoptionDate.Value)
            yield return new ValidationResult(
                "Дата вступления в силу не может быть раньше даты принятия",
                [nameof(EffectiveDate)]);
    }
}