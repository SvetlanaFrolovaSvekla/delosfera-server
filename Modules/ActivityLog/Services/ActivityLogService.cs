using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.DTO.Request;
using delosfera_server.Modules.ActivityLog.DTO.Response;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.ActivityLog.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ActivityLogService(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

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
        // Заявки на замещение ведутся в разделе СЗ (УЧР) — и в журнале идут под вкладкой «СЗ».
        // Раньше их в журнале не было вовсе: ни создания, ни согласования.
        (ActivityModules.Sz, ["Sz", "SubstitutionRequest"]),
        (ActivityModules.Procurement, ["ProcurementRequest"]),
        (ActivityModules.Acknowledgement, ["AcknowledgementSheet"]),
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
                .Where(x => x.Module == ActivityModules.Vnd
                            && x.Kind != ActivityEventKind.ActualizationReminderSent)
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var draftVisibility = await LoadDraftVisibilityAsync(entries.Select(x => x.EntityId));
            result.AddRange(entries.Select(x => ToResponse(x, languageCode, CanOpenVndEntry(x.EntityId, draftVisibility))));
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

        return rows.Select(a => AuditToResponse(module, a, actors)).ToList();
    }

    /// <summary>Строка аудита (СЗ/закупки) в виде записи журнала.</summary>
    private static ActivityLogEntryResponse AuditToResponse(string module, AuditEntry a, IReadOnlyDictionary<int, string> actors)
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
            // Префикс с «/» на конце — карточка по id; без него — общая страница раздела
            // (у листа ознакомления своей карточки нет). Раньше id приклеивался всегда, и
            // такие ссылки вели в никуда ("/management/substitutions12").
            Url = urlPrefix.EndsWith('/') ? $"{urlPrefix}{a.EntityId}" : urlPrefix,
            CreatedAt = a.At,
        };
    }

    // ── Страница "Активность на портале": поиск, период, тип события, подгрузка ──────────

    /// <summary>Сколько строк аудита СЗ/закупок просматриваем за один запрос, когда отбор по
    /// тексту/типу приходится делать в памяти (текст этих записей собирается на лету, в базе
    /// его нет). Дальше — ищем на следующей странице ("Показать ещё").</summary>
    private const int AuditScanBatch = 500;
    private const int AuditScanMax = 5000;

    public async Task<ActivityFeedResponse> GetFeedAsync(
        ActivityFeedRequest request, string languageCode, CancellationToken ct = default)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);
        var module = string.IsNullOrWhiteSpace(request.Module) ? null : request.Module.Trim();
        var terms = SplitTerms(request.Q);
        var icons = (request.Icons ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(i => i.ToLowerInvariant())
            .ToHashSet();
        var fromUtc = request.From?.UtcDateTime;
        var toUtc = request.To?.UtcDateTime;
        DateTime? before = long.TryParse(request.Cursor, out var ticks) && ticks > 0
            ? new DateTime(ticks, DateTimeKind.Utc)
            : null;

        var result = new List<ActivityLogEntryResponse>();

        if (module is null || module == ActivityModules.Vnd)
        {
            var query = _db.Set<ActivityLogEntry>().AsNoTracking()
                .Where(x => x.Module == ActivityModules.Vnd
                            && x.Kind != ActivityEventKind.ActualizationReminderSent);
            if (before is { } b) query = query.Where(x => x.CreatedAt < b);
            if (fromUtc is { } f) query = query.Where(x => x.CreatedAt >= f);
            if (toUtc is { } t) query = query.Where(x => x.CreatedAt <= t);
            if (icons.Count > 0)
            {
                var kinds = Enum.GetValues<ActivityEventKind>().Where(k => icons.Contains(MapIcon(k))).ToList();
                query = query.Where(x => kinds.Contains(x.Kind));
            }
            foreach (var term in terms)
            {
                // Ищем во всех трёх языках - запись должна находиться, на каком бы языке её ни читали
                var pattern = $"%{EscapeLike(term)}%";
                query = query.Where(x => EF.Functions.ILike(x.TextRu, pattern, "\\")
                                         || (x.TextEn != null && EF.Functions.ILike(x.TextEn, pattern, "\\"))
                                         || (x.TextKg != null && EF.Functions.ILike(x.TextKg, pattern, "\\")));
            }

            var entries = await query
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToListAsync(ct);

            var draftVisibility = await LoadDraftVisibilityAsync(entries.Select(x => x.EntityId));
            result.AddRange(entries.Select(x => ToResponse(x, languageCode, CanOpenVndEntry(x.EntityId, draftVisibility))));
        }

        foreach (var (mod, entityTypes) in AuditSlices)
        {
            if (module is not null && module != mod) continue;
            result.AddRange(await FeedFromAuditAsync(mod, entityTypes, limit + 1, terms, icons, fromUtc, toUtc, before, ct));
        }

        var ordered = result.OrderByDescending(x => x.CreatedAt).ToList();
        var page = ordered.Take(limit).ToList();
        return new ActivityFeedResponse
        {
            Items = page,
            HasMore = ordered.Count > limit,
            NextCursor = ordered.Count > limit && page.Count > 0
                ? DateTime.SpecifyKind(page[^1].CreatedAt, DateTimeKind.Utc).Ticks.ToString()
                : null,
        };
    }

    /// <summary>СЗ/закупки из технического аудита с тем же отбором. Период и курсор - в базе, а
    /// текст и тип события известны только после AuditActivityText.Describe - их проверяем в
    /// памяти, просматривая аудит пачками (не больше AuditScanMax строк за запрос).</summary>
    private async Task<List<ActivityLogEntryResponse>> FeedFromAuditAsync(
        string module, string[] entityTypes, int take, IReadOnlyList<string> terms, HashSet<string> icons,
        DateTime? fromUtc, DateTime? toUtc, DateTime? before, CancellationToken ct)
    {
        var baseQuery = _db.AuditEntries.AsNoTracking().Where(a => entityTypes.Contains(a.EntityType));
        if (before is { } b) baseQuery = baseQuery.Where(a => a.At < b);
        if (fromUtc is { } f) baseQuery = baseQuery.Where(a => a.At >= f);
        if (toUtc is { } t) baseQuery = baseQuery.Where(a => a.At <= t);

        var filterInMemory = terms.Count > 0 || icons.Count > 0;
        var result = new List<ActivityLogEntryResponse>();
        var actors = new Dictionary<int, string>();
        DateTime? lastAt = null;
        long lastId = 0;
        var scanned = 0;

        while (true)
        {
            var query = baseQuery;
            if (lastAt is { } la)
            {
                var lid = lastId;
                query = query.Where(a => a.At < la || (a.At == la && a.Id < lid));
            }

            var rows = await query
                .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
                .Take(filterInMemory ? AuditScanBatch : take)
                .ToListAsync(ct);
            if (rows.Count == 0) break;
            scanned += rows.Count;

            var missing = rows.Where(r => r.UserId is { } id && !actors.ContainsKey(id))
                .Select(r => r.UserId!.Value).Distinct().ToList();
            if (missing.Count > 0)
            {
                var names = await _db.Users.AsNoTracking()
                    .Where(u => missing.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToListAsync(ct);
                foreach (var n in names) actors[n.Id] = n.FullName;
            }

            foreach (var row in rows)
            {
                var entry = AuditToResponse(module, row, actors);
                if (icons.Count > 0 && !icons.Contains(entry.Icon)) continue;
                if (terms.Count > 0)
                {
                    var text = entry.Text.ToLowerInvariant();
                    if (!terms.All(term => text.Contains(term))) continue;
                }
                result.Add(entry);
                if (result.Count >= take) return result;
            }

            if (!filterInMemory || rows.Count < AuditScanBatch || scanned >= AuditScanMax) break;
            lastAt = rows[^1].At;
            lastId = rows[^1].Id;
        }

        return result;
    }

    private static List<string> SplitTerms(string? q) =>
        (q ?? "").ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();

    /// <summary>Экранирование для ILIKE (escape-символ - обратный слэш), чтобы "%" и "_" в
    /// запросе искались как обычные символы.</summary>
    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Весь журнал активности по одному документу — не "последние N" для дашборда
    /// (см. GetRecentAsync), а полностью. Для таба "История" на карточке документа: там нужен
    /// весь накопленный аудит по этой ВНД (или другому документу модуля), без ограничения.</summary>
    public async Task<List<ActivityLogEntryResponse>> GetByEntityAsync(
        string module, int entityId, string languageCode)
    {
        // Сюда попадают только через уже открытую карточку документа (вкладка "История") —
        // право на неё (в т.ч. видимость чужого черновика) уже проверено при её открытии
        // (см. VndService.GetByIdAsync), поэтому здесь CanOpen не пересчитываем - остаётся
        // true по умолчанию (см. GetRecentAsync выше, где это как раз нужно).
        var entries = await _db.Set<ActivityLogEntry>()
            .Where(x => x.Module == module && x.EntityId == entityId
                        && x.Kind != ActivityEventKind.ActualizationReminderSent)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return entries.Select(x => ToResponse(x, languageCode)).ToList();
    }

    /// <summary>Статус и автор ВНД по id — только то, что нужно для проверки видимости
    /// черновика (см. CanOpenVndEntry), одним запросом на все записи разом.</summary>
    private async Task<Dictionary<int, (VndStatus Status, int? CreatedByUserId)>> LoadDraftVisibilityAsync(
        IEnumerable<int> vndIds)
    {
        var ids = vndIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, (VndStatus, int?)>();

        return await _db.VndDocuments
            .Where(v => ids.Contains(v.Id))
            .Select(v => new {v.Id, v.Status, v.CreatedByUserId})
            .ToDictionaryAsync(v => v.Id, v => (v.Status, v.CreatedByUserId));
    }

    /// <summary>Тот же критерий видимости черновика, что и в VndService.GetByIdAsync (см.
    /// подробный комментарий там): свой черновик, право ViewOtherUsersDrafts, либо ВНД уже
    /// не черновик - открыть можно. Запись о ВНД, которого не нашли (например, черновик с тех
    /// пор удалили) - тоже true: тогда переход по ссылке упрётся в обычное "не найдено", а не
    /// в ошибку доступа, так что скрывать её незачем.</summary>
    private bool CanOpenVndEntry(int vndId, Dictionary<int, (VndStatus Status, int? CreatedByUserId)> draftVisibility)
    {
        if (!draftVisibility.TryGetValue(vndId, out var info)) return true;

        return info.Status != VndStatus.Draft
               || info.CreatedByUserId == _currentUser.UserId
               || _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts);
    }

    private static ActivityLogEntryResponse ToResponse(ActivityLogEntry x, string languageCode, bool canOpen = true) => new()
    {
        Id = x.Id,
        Module = x.Module,
        EntityId = x.EntityId,
        EntityCode = x.EntityCode,
        Icon = MapIcon(x.Kind),
        Kind = ToCamelCase(x.Kind.ToString()),
        Text = languageCode switch
        {
            "en" => string.IsNullOrWhiteSpace(x.TextEn) ? x.TextRu : x.TextEn,
            "kg" => string.IsNullOrWhiteSpace(x.TextKg) ? x.TextRu : x.TextKg,
            _ => x.TextRu
        },
        Url = x.Url,
        CreatedAt = x.CreatedAt,
        CanOpen = canOpen
    };

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

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
        // правка документа, как и "edit" у СЗ/закупок из технического аудита. Добавление/
        // удаление согласующего главным редактором — правка маршрута, тот же значок.
        ActivityEventKind.RequisitesUpdated
            or ActivityEventKind.Resubmitted
            or ActivityEventKind.RedactionEdited
            or ActivityEventKind.ApproverAdded
            or ActivityEventKind.ApproverRemoved => "edit",
        // Удаление черновика — красная иконка-мусорка, чтобы отличать от простой "правки".
        ActivityEventKind.DraftDeleted => "trash",
        _ => "info"
    };
}