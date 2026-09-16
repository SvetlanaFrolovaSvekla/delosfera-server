using delosfera_server.Common.Extensions;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Notifications.DTO.Response;

namespace delosfera_server.Modules.Notifications.Models;

public class NotificationCategoryDescription : ITranslatableEntity
{
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
}

public static class NotificationCategoryCatalog
{
    // Порядок словаря - это порядок вкладок категорий на фронте (см. GetAll ниже).
    // Approval и Task сюда сознательно не входят: своей вкладки у них больше нет,
    // их уведомления теперь идут по ВНД/СЗ/закупкам либо в "Разное" (см. NotificationCategory).
    public static readonly Dictionary<NotificationCategory, NotificationCategoryDescription> Descriptions = new()
    {
        [NotificationCategory.System] = new NotificationCategoryDescription
        {
            TitleRu = "Системные", TitleEn = "System", TitleKg = "Системалык"
        },
        [NotificationCategory.Vnd] = new NotificationCategoryDescription
        {
            TitleRu = "ВНД", TitleEn = "VND", TitleKg = "ВНД"
        },
        [NotificationCategory.Sz] = new NotificationCategoryDescription
        {
            TitleRu = "СЗ", TitleEn = "Memos", TitleKg = "Кызматтык каттар"
        },
        [NotificationCategory.Procurement] = new NotificationCategoryDescription
        {
            TitleRu = "Закупки", TitleEn = "Procurement", TitleKg = "Сатып алуулар"
        },
        [NotificationCategory.Other] = new NotificationCategoryDescription
        {
            TitleRu = "Разное", TitleEn = "Other", TitleKg = "Башка"
        }
    };

    public static List<NotificationCategoryResponse> GetAll(string languageCode) =>
        Descriptions.Select(kv => new NotificationCategoryResponse
        {
            Code = (int)kv.Key,
            Key = kv.Key.ToString(),
            Name = kv.Value.ResolveTitle(languageCode)
        }).ToList();
}