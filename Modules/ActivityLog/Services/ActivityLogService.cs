using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.DTO.Response;
using delosfera_server.Modules.ActivityLog.Models;

namespace delosfera_server.Modules.ActivityLog.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly DelosferaDbContext _db;
    public ActivityLogService(DelosferaDbContext db) => _db = db;

    /// <summary>Добавляет запись в контекст без SaveChanges - вызывающий сервис сохраняет её
    /// вместе со своими изменениями, одной транзакцией</summary>
    public void Log(string module, ActivityEventKind kind, int entityId, string entityCode,
        int? actorUserId, ActivityText text, string url)
    {
        _db.Set<ActivityLogEntry>().Add(new ActivityLogEntry
        {
            Module = module,
            EntityId = entityId,
            EntityCode = entityCode,
            Kind = kind,
            ActorUserId = actorUserId,
            TextRu = text.Ru,
            TextEn = text.En,
            TextKg = text.Kg,
            Url = url
        });
    }

    // Получение последних записей журнала активности
    public async Task<List<ActivityLogEntryResponse>> GetRecentAsync(
        int limit, string languageCode, string? module = null)
    {
        var query = _db.Set<ActivityLogEntry>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(x => x.Module == module);

        var entries = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return entries.Select(x => ToResponse(x, languageCode)).ToList();
    }

    /// <summary>Весь журнал активности по одному документу — не "последние N" для дашборда
    /// (см. GetRecentAsync), а полностью. Для таба "История" на карточке документа: там нужен
    /// весь накопленный аудит по этой ВНД (или другому документу модуля), без ограничения.</summary>
    public async Task<List<ActivityLogEntryResponse>> GetByEntityAsync(
        string module, int entityId, string languageCode)
    {
        var entries = await _db.Set<ActivityLogEntry>()
            .Where(x => x.Module == module && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return entries.Select(x => ToResponse(x, languageCode)).ToList();
    }

    private static ActivityLogEntryResponse ToResponse(ActivityLogEntry x, string languageCode) => new()
    {
        Id = x.Id,
        Module = x.Module,
        EntityId = x.EntityId,
        EntityCode = x.EntityCode,
        Icon = MapIcon(x.Kind),
        Text = languageCode switch
        {
            "en" => string.IsNullOrWhiteSpace(x.TextEn) ? x.TextRu : x.TextEn,
            "kg" => string.IsNullOrWhiteSpace(x.TextKg) ? x.TextRu : x.TextKg,
            _ => x.TextRu
        },
        Url = x.Url,
        CreatedAt = x.CreatedAt
    };

    // Вспомогательный метод для маппинга иконок
    private static string MapIcon(ActivityEventKind kind) => kind switch
    {
        ActivityEventKind.Approved
            or ActivityEventKind.ApprovedWithComment
            or ActivityEventKind.Published
            or ActivityEventKind.Finalized => "check",
        ActivityEventKind.Rejected
            or ActivityEventKind.AutoApprovedTimeout
            or ActivityEventKind.RevisionNeeded => "x",
        ActivityEventKind.Created
            or ActivityEventKind.ItemAdded
            or ActivityEventKind.ProcessStarted => "doc",
        ActivityEventKind.HoldStarted => "clock",
        _ => "info"
    };
}