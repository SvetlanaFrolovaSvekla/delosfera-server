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

    /// <summary>Иконки, которые понимает виджет «Последняя активность»; прочие
    /// сводятся к нейтральной.</summary>
    private static readonly HashSet<string> WidgetIcons = ["check", "x", "doc", "clock", "edit", "info"];

    /// <summary>Какие типы аудита относятся к какому разделу дашборда. Берём только
    /// корневую запись контура: её id совпадает с id карточки в интерфейсе, поэтому
    /// ссылка ведёт куда надо (дочерние сущности живут под своими id).</summary>
    private static readonly (string Module, string[] EntityTypes)[] AuditSlices =
    [
        (ActivityModules.Sz, ["Sz"]),
        (ActivityModules.Procurement, ["ProcurementRequest"]),
    ];

    // Получение последних записей журнала активности по всем контурам.
    //
    // ВНД ведёт собственный человекочитаемый поток в таблице журнала. СЗ и закупки
    // такого потока не ведут — их события берутся из технического аудита и
    // превращаются в строки журнала на лету (как история документа), иначе на
    // дашборде были бы видны только события ВНД.
    public async Task<List<ActivityLogEntryResponse>> GetRecentAsync(
        int limit, string languageCode, string? module = null)
    {
        var result = new List<ActivityLogEntryResponse>();

        if (module is null || module == ActivityModules.Vnd)
        {
            var entries = await _db.Set<ActivityLogEntry>()
                .Where(x => x.Module == ActivityModules.Vnd)
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToListAsync();
            result.AddRange(entries.Select(x => ToResponse(x, languageCode)));
        }

        foreach (var (mod, entityTypes) in AuditSlices)
        {
            if (module is not null && module != mod) continue;
            result.AddRange(await RecentFromAuditAsync(mod, entityTypes, limit));
        }

        return result
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Последние события контура из технического аудита, оформленные как строки
    /// журнала. Тексты — русские (аудит другого языка не хранит), как и в истории
    /// документа; локализованный поток есть только у ВНД.
    /// </summary>
    private async Task<List<ActivityLogEntryResponse>> RecentFromAuditAsync(
        string module, string[] entityTypes, int limit)
    {
        var rows = await _db.AuditEntries.AsNoTracking()
            .Where(a => entityTypes.Contains(a.EntityType))
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .Take(limit)
            .ToListAsync();

        if (rows.Count == 0) return [];

        var actorIds = rows.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var actors = await _db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return rows.Select(a =>
        {
            var (_, urlPrefix) = AuditActivityText.Origin(a.EntityType);
            var (ru, icon) = AuditActivityText.Describe(a.EntityType, a.Action);
            var actor = a.UserId is { } uid && actors.TryGetValue(uid, out var name) ? name : "Система";

            return new ActivityLogEntryResponse
            {
                // Id аудита — long; в отклике он лишь ключ строки, переполнение при
                // сужении не влияет на отображение.
                Id = unchecked((int)a.Id),
                Module = module,
                EntityId = a.EntityId,
                EntityCode = "",
                Icon = WidgetIcons.Contains(icon) ? icon : "info",
                // «Иванов зарегистрировал записку»; у системного события подлежащее — «Система».
                Text = $"{actor} {ru}",
                Url = $"{urlPrefix}{a.EntityId}",
                CreatedAt = a.At,
            };
        }).ToList();
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
        // Смена реквизитов и повторная отправка исправленной редакции — это
        // правка документа, как и "edit" у СЗ/закупок из технического аудита.
        ActivityEventKind.RequisitesUpdated
            or ActivityEventKind.Resubmitted => "edit",
        _ => "info"
    };
}