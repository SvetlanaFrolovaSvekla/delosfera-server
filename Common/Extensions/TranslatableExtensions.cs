using delosfera_server.Common.Models;

namespace delosfera_server.Common.Extensions;

public static class TranslatableExtensions
{
    public static string ResolveTitle(this ITranslatableEntity entity, string languageCode) => languageCode switch
    {
        "en" => string.IsNullOrWhiteSpace(entity.TitleEn) ? entity.TitleRu : entity.TitleEn,
        "kg" => string.IsNullOrWhiteSpace(entity.TitleKg) ? entity.TitleRu : entity.TitleKg,
        _ => entity.TitleRu
    };
}