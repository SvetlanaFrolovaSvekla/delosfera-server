using delosfera_server.Modules.ActivityLog.DTO.Request;
using delosfera_server.Modules.ActivityLog.DTO.Response;
using delosfera_server.Modules.ActivityLog.Models;

namespace delosfera_server.Modules.ActivityLog.Services;

public interface IActivityLogService
{
    /// <summary>Добавляет запись в контекст без SaveChanges - вызывающий сервис сохраняет её
    /// вместе со своими изменениями, одной транзакцией</summary>
    void Log(string module, ActivityEventKind kind, int entityId, string entityCode,
        int? actorUserId, ActivityText text, string url);

    // Получение последних записей журнала активности
    Task<List<ActivityLogEntryResponse>> GetRecentAsync(int limit, string languageCode, string? module = null);

    /// <summary>Весь журнал активности по одному документу — не "последние N" для дашборда
    /// (см. GetRecentAsync), а полностью. Для таба "История" на карточке документа.</summary>
    Task<List<ActivityLogEntryResponse>> GetByEntityAsync(string module, int entityId, string languageCode);

    /// <summary>Страница "Активность на портале": поиск, период, тип события, раздел и
    /// постраничная подгрузка по курсору.</summary>
    Task<ActivityFeedResponse> GetFeedAsync(ActivityFeedRequest request, string languageCode, CancellationToken ct = default);
}