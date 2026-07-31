using delosfera_server.Modules.Notifications.Models;

namespace delosfera_server.Modules.Notifications.DTO.Request;

public class NotificationFilterRequest
{
    public List<NotificationCategory> Categories { get; set; } = [];

    /// <summary>null - все, true - только прочитанные, false - только непрочитанные</summary>
    public bool? IsRead { get; set; }

    /// <summary>true - только избранные</summary>
    public bool? IsFavorite { get; set; }
    
    // Фильтрация по серьезности (типу)
    public List<NotificationSeverity> Severities { get; set; } = [];

    /// <summary>Поиск по заголовку/тексту (на языке запроса)</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}