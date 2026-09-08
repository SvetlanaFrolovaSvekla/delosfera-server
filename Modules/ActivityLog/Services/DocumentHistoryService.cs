using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.ActivityLog.Services;

/// <summary>Одна строка истории документа.</summary>
public class DocumentHistoryEntry
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string Text { get; set; } = "";
    public string Icon { get; set; } = "info";

    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = "Система";

    public DateTime At { get; set; }
}

public interface IDocumentHistoryService
{
    /// <summary>
    /// История одного документа из технического аудита. Дополнительные типы —
    /// дочерние сущности того же документа (поручения записки, вложения), чтобы
    /// их события попадали в ту же ленту.
    /// </summary>
    Task<List<DocumentHistoryEntry>> ForAsync(
        string entityType, int entityId,
        IEnumerable<(string Type, int Id)>? related = null,
        CancellationToken ct = default);
}

/// <summary>
/// История действий по документу поверх технического аудита.
///
/// Аудит писали все контуры, а показывали только ВНД — у него был отдельный
/// человекочитаемый поток. Здесь журнал строится из того же аудита на лету,
/// поэтому историю получает каждый контур сразу, без дописывания вызовов.
/// </summary>
public class DocumentHistoryService : IDocumentHistoryService
{
    private readonly DelosferaDbContext _db;

    public DocumentHistoryService(DelosferaDbContext db) => _db = db;

    public async Task<List<DocumentHistoryEntry>> ForAsync(
        string entityType, int entityId,
        IEnumerable<(string Type, int Id)>? related = null,
        CancellationToken ct = default)
    {
        // Пары «тип+id», за которыми тянем аудит: сам документ и его дочерние
        // сущности. Кортежи EF в Where не разложит, поэтому фильтруем в два
        // приёма и объединяем в памяти — записей на один документ немного.
        var pairs = new List<(string Type, int Id)> {(entityType, entityId)};
        if (related is not null) pairs.AddRange(related);

        var types = pairs.Select(p => p.Type).Distinct().ToList();
        var ids = pairs.Select(p => p.Id).Distinct().ToList();

        var rows = await _db.AuditEntries.AsNoTracking()
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        // Отсекаем ложные совпадения по чужому id того же набора: берём только
        // реально запрошенные пары.
        var wanted = pairs.ToHashSet();
        rows = rows.Where(a => wanted.Contains((a.EntityType, a.EntityId))).ToList();

        var actorIds = rows.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var actors = await _db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(a =>
        {
            var (ru, icon) = AuditActivityText.Describe(a.EntityType, a.Action);
            var actor = a.UserId is { } uid && actors.TryGetValue(uid, out var name) ? name : "Система";

            return new DocumentHistoryEntry
            {
                Id = a.Id,
                Action = a.Action,
                // «Иванов зарегистрировал записку»; у системного события подлежащее — «Система».
                Text = $"{actor} {ru}",
                Icon = icon,
                ActorUserId = a.UserId,
                ActorName = actor,
                At = a.At,
            };
        }).ToList();
    }
}
