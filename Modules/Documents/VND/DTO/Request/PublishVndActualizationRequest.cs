using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class PublishVndActualizationRequest : IValidatableObject
{
    public required bool HadChanges { get; set; }
    public DateOnly? NewDueActualizationDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewDueActualizationDate.HasValue &&
            NewDueActualizationDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new ValidationResult(
                "Новая дата актуализации должна быть в будущем!",
                [nameof(NewDueActualizationDate)]);
    }
}