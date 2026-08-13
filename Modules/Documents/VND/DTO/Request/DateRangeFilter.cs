using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Фильтр по дате — точная или диапазон</summary>
public class DateRangeFilter : IValidatableObject
{
    public DateOnly? Exact { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            yield return new ValidationResult(
                "Начало диапазона не может быть позже конца",
                [nameof(From), nameof(To)]);
    }
}