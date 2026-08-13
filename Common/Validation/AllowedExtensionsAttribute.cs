using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Common.Validation;

public class AllowedExtensionsAttribute : ValidationAttribute
{
    private readonly string[] _extensions;

    public AllowedExtensionsAttribute(params string[] extensions)
    {
        _extensions = extensions.Select(e => e.ToLowerInvariant()).ToArray();
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is null) return ValidationResult.Success;

        if (value is IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_extensions.Contains(ext))
                return new ValidationResult(
                    $"Недопустимое расширение «{ext}». Разрешено: {string.Join(", ", _extensions)}");
        }

        if (value is IEnumerable<IFormFile> files)
        {
            foreach (var f in files)
            {
                var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                if (!_extensions.Contains(ext))
                    return new ValidationResult(
                        $"Файл «{f.FileName}»: недопустимое расширение. Разрешено: {string.Join(", ", _extensions)}");
            }
        }

        return ValidationResult.Success;
    }
}