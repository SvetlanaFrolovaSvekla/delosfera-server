using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Common.Validation;

// Для списков (List<int> и т.д.)
public class MaxCountAttribute : ValidationAttribute
{
    private readonly int _max;
    public MaxCountAttribute(int max) => _max = max;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is System.Collections.ICollection list && list.Count > _max)
            return new ValidationResult($"Максимум {_max} элементов, передано {list.Count}");
        return ValidationResult.Success;
    }
}