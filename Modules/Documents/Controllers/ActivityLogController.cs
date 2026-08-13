using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>Событие ленты активности в том виде, в каком его показывает рабочий стол.</summary>
public class ActivityLogEntryResponse
{
    public long Id { get; set; }

    /// <summary>Контур события: vnd, sz, prc.</summary>
    public required string Module { get; set; }

    public int EntityId { get; set; }

    /// <summary>Номер или обозначение записи — по нему человек узнаёт документ.</summary>
    public required string EntityCode { get; set; }

    /// <summary>Значок: check, x, doc, clock, info.</summary>
    public required string Icon { get; set; }

    public required string Text { get; set; }

    /// <summary>Куда ведёт событие.</summary>
    public required string Url { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Лента последних событий пользователя на рабочем столе.
///
/// Собирается из журнала аудита: он и так пишется по каждому действию, и
/// отдельного хранилища для ленты заводить незачем. Показываются только
/// собственные действия — лента отвечает на вопрос «что я делал последним»,
/// а не заменяет журнал аудита, доступ к которому у безопасности.
/// </summary>
[ApiController]
[Route("api/activity-log")]
[Tags("Активность")]
[Authorize]
public class ActivityLogController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ActivityLogController(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Последние события текущего пользователя.</summary>
    [HttpGet("recent")]
    public async Task<IActionResult> Recent(
        [FromQuery] int limit = 8, [FromQuery] string? module = null, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var entries = await _db.AuditEntries
            .AsNoTracking()
            .Where(e => e.UserId == _currentUser.UserId)
            .OrderByDescending(e => e.At)
            // Берём с запасом: часть записей относится к служебным сущностям и
            // до ленты не дойдёт, иначе она окажется короче запрошенного.
            .Take(limit * 5)
            .ToListAsync(ct);

        var documentIds = entries
            .Where(e => e.EntityType == "Document")
            .Select(e => e.EntityId)
            .Distinct()
            .ToList();

        var szIds = entries
            .Where(e => e.EntityType == "Sz")
            .Select(e => e.EntityId)
            .Distinct()
            .ToList();

        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => new DocumentBrief(d.Id, d.RegNumber, d.Title, d.Type))
            .ToDictionaryAsync(d => d.Id, ct);

        var memos = await _db.SzDocuments
            .AsNoTracking()
            .Where(s => szIds.Contains(s.Id))
            .Select(s => new MemoBrief(s.Id, s.Document!.RegNumber, s.Document.Title))
            .ToDictionaryAsync(s => s.Id, ct);

        var items = new List<ActivityLogEntryResponse>();

        foreach (var entry in entries)
        {
            var item = Describe(entry, documents, memos);
            if (item is null) continue;
            if (module is not null && !item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)) continue;

            items.Add(item);
            if (items.Count == limit) break;
        }

        return Ok(items);
    }

    private static ActivityLogEntryResponse? Describe(
        AuditEntry entry,
        IReadOnlyDictionary<int, DocumentBrief> documents,
        IReadOnlyDictionary<int, MemoBrief> memos)
    {
        string module;
        string code;
        string url;

        switch (entry.EntityType)
        {
            case "Sz" when memos.TryGetValue(entry.EntityId, out var memo):
                module = "sz";
                code = memo.RegNumber ?? memo.Title ?? $"записка № {entry.EntityId}";
                url = $"/sz/{entry.EntityId}";
                break;

            // Записка пишет в журнал дважды — как документ и как запись контура.
            // В ленте это выглядело бы одним и тем же событием подряд, поэтому
            // документ служебной записки пропускаем: её событие уже учтено выше.
            case "Document" when documents.TryGetValue(entry.EntityId, out var document)
                                 && document.Type != DocumentType.Sz:
                module = document.Type == DocumentType.Procurement ? "prc" : "vnd";
                code = document.RegNumber ?? document.Title ?? $"документ № {entry.EntityId}";
                url = module == "prc" ? "/prc" : $"/base-vnd/{entry.EntityId}";
                break;

            default:
                // Служебные сущности — настройки, роли, вложения — в ленте не нужны:
                // человек ищет там свои документы, а не следы внутренних операций.
                return null;
        }

        var (icon, action) = Action(entry.Action);

        return new ActivityLogEntryResponse
        {
            Id = entry.Id,
            Module = module,
            EntityId = entry.EntityId,
            EntityCode = code,
            Icon = icon,
            Text = $"{action}: {code}",
            Url = url,
            CreatedAt = entry.At,
        };
    }

    /// <summary>Документ в объёме, нужном ленте.</summary>
    private sealed record DocumentBrief(int Id, string? RegNumber, string? Title, DocumentType Type);

    /// <summary>Служебная записка в объёме, нужном ленте.</summary>
    private sealed record MemoBrief(int Id, string? RegNumber, string? Title);

    /// <summary>Значок и человеческое название действия.</summary>
    private static (string Icon, string Text) Action(string action) => action switch
    {
        "Created" => ("doc", "Создано"),
        "Updated" => ("doc", "Изменено"),
        "Submitted" => ("clock", "Отправлено на согласование"),
        "Registered" => ("doc", "Зарегистрировано"),
        "Approved" => ("check", "Согласовано"),
        "Rejected" => ("x", "Отклонено"),
        "Withdrawn" => ("x", "Отозвано"),
        "AddresseeDecided" => ("check", "Вынесено решение"),
        "Resolution" => ("check", "Выдана резолюция"),
        "StatusFromRoute" => ("clock", "Изменён статус"),
        "Executed" => ("check", "Исполнено"),
        "Deleted" => ("x", "Удалено"),
        _ => ("info", action),
    };
}
