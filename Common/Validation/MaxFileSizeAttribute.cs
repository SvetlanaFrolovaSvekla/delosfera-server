using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Common.Validation;

public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly long _maxBytes;

    public MaxFileSizeAttribute(long maxMegabytes)
    {
        _maxBytes = maxMegabytes * 1024 * 1024;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is null) return ValidationResult.Success;

        if (value is IFormFile file)
        {
            if (file.Length == 0)
                return new ValidationResult("Файл пустой");
            if (file.Length > _maxBytes)
                return new ValidationResult($"Файл превышает допустимый размер ({_maxBytes / 1024 / 1024} МБ)");
        }

        if (value is IEnumerable<IFormFile> files)
        {
            foreach (var f in files)
            {
                if (f.Length == 0) return new ValidationResult("Один из файлов пустой");
                if (f.Length > _maxBytes)
                    return new ValidationResult($"Файл «{f.FileName}» превышает {_maxBytes / 1024 / 1024} МБ");
            }
        }

        return ValidationResult.Success;
    }
}