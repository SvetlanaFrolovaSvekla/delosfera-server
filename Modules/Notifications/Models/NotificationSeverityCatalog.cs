using delosfera_server.Common.Extensions;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Notifications.DTO.Response;

namespace delosfera_server.Modules.Notifications.Models;

public class NotificationSeverityDescription : ITranslatableEntity
{
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
}

public static class NotificationSeverityCatalog
{
    public static readonly Dictionary<NotificationSeverity, NotificationSeverityDescription> Descriptions = new()
    {
        [NotificationSeverity.Info] = new NotificationSeverityDescription
        {
            TitleRu = "Информация", TitleEn = "Info", TitleKg = "Маалымат"
        },
        [NotificationSeverity.Success] = new NotificationSeverityDescription
        {
            TitleRu = "Успех", TitleEn = "Success", TitleKg = "Ийгилик"
        },
        [NotificationSeverity.Warning] = new NotificationSeverityDescription
        {
            TitleRu = "Предупреждение", TitleEn = "Warning", TitleKg = "Эскертүү"
        },
        [NotificationSeverity.Urgent] = new NotificationSeverityDescription
        {
            TitleRu = "Срочно", TitleEn = "Urgent", TitleKg = "Шашылыш"
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