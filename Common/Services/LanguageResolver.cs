namespace delosfera_server.Common.Services;

public interface ILanguageResolver
{
    string Resolve(HttpRequest request);
}

public class LanguageResolver : ILanguageResolver
{
    private static readonly HashSet<string> SupportedLanguages = new() { "ru", "en", "kg" };
    private const string DefaultLanguage = "ru";

    public string Resolve(HttpRequest request)
    {
        // Приоритет: кастомный заголовок X-Language, иначе Accept-Language, иначе дефолт
        var language = request.Headers["X-Language"].FirstOrDefault()
                       ?? request.Headers["Accept-Language"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();

        return SupportedLanguages.Contains(language ?? "") ? language! : DefaultLanguage;
    }
}