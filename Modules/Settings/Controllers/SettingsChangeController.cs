using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Settings.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Settings.Controllers;

/// <summary>
/// Журнал изменений настроек и справочников.
///
/// Отвечает на вопрос, который задают редко и всегда задним числом: кто и когда
/// поменял справочник, из-за которого всё поехало. Отдельно от журнала действий
/// по документам — там ищут след записки, здесь след настройки, и мешать их
/// значит не найти ни того, ни другого.
/// </summary>
[ApiController]
[Authorize]
[Route("api/settings/changes")]
[Tags("Журнал изменений настроек")]
public class SettingsChangeController : ControllerBase
{
    private readonly DelosferaDbContext _db;

    public SettingsChangeController(DelosferaDbContext db) => _db = db;

    /// <summary>Журнал с отбором по области, автору и периоду.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Index(
        [FromQuery] string? area,
        [FromQuery] int? userId,
        [FromQuery] SettingsChangeKind? kind,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? text,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.SettingsChanges.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(area)) query = query.Where(c => c.Area == area);
        if (userId is { } author) query = query.Where(c => c.UserId == author);
        if (kind is { } k) query = query.Where(c => c.Kind == k);

        if (from is { } start)
            query = query.Where(c => c.At >= start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (to is { } end)
            query = query.Where(c => c.At <= end.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (!string.IsNullOrWhiteSpace(text))
        {
            var needle = text.Trim();
            query = query.Where(c =>
                (c.EntityTitle != null && EF.Functions.ILike(c.EntityTitle, $"%{needle}%"))
                || EF.Functions.ILike(c.Area, $"%{needle}%"));
        }

        var total = await query.CountAsync(ct);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var rows = await query
            .OrderByDescending(c => c.At).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Area,
                c.EntityType,
                c.EntityId,
                c.EntityTitle,
                Kind = c.Kind.ToString(),
                c.At,
                c.UserId,
                c.UserName,
                c.ChangesJson,
            })
            .ToListAsync(ct);

        // Имена для записей, где снимок не сохранился. Одним запросом на страницу,
        // а не связью в таблице: связь роняла бы сохранение справочника, если
        // учётной записи в базе уже нет.
        var missing = rows
            .Where(r => r.UserName is null && r.UserId is not null)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();

        var names = missing.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => missing.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // Разбираем изменения на сервере: клиенту иначе пришлось бы разбирать
        // строку самому, и одна испорченная запись ломала бы всю страницу.
        var items = rows.Select(r => new
        {
            r.Id, r.Area, r.EntityType, r.EntityId, r.EntityTitle, r.Kind, r.At,
            author = r.UserName ?? (r.UserId is {} id ? names.GetValueOrDefault(id) : null),
            changes = ParseChanges(r.ChangesJson),
        });

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Области, по которым есть записи — для выпадающего списка отбора.</summary>
    [HttpGet("areas")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Areas(CancellationToken ct)
    {
        var rows = await _db.SettingsChanges.AsNoTracking()
            .GroupBy(c => c.Area)
            .Select(g => new { area = g.Key, count = g.Count(), last = g.Max(c => c.At) })
            .OrderByDescending(x => x.last)
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>История одной записи справочника — что с ней делали за всё время.</summary>
    [HttpGet("entity/{entityType}/{entityId:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Entity(string entityType, int entityId, CancellationToken ct)
    {
        var rows = await _db.SettingsChanges.AsNoTracking()
            .Where(c => c.EntityType == entityType && c.EntityId == entityId)
            .OrderByDescending(c => c.At)
            .Select(c => new
            {
                c.Id,
                Kind = c.Kind.ToString(),
                c.At,
                c.UserId,
                c.UserName,
                c.ChangesJson,
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => new
        {
            r.Id, r.Kind, r.At,
            author = r.UserName,
            changes = ParseChanges(r.ChangesJson),
        }));
    }

    private static object[] ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            return doc.RootElement.EnumerateArray()
                .Select(e => (object)new
                {
                    field = e.TryGetProperty("Field", out var f) ? f.GetString() : null,
                    before = e.TryGetProperty("Before", out var b) ? b.GetString() : null,
                    after = e.TryGetProperty("After", out var a) ? a.GetString() : null,
                })
                .ToArray();
        }
        catch (JsonException)
        {
            // Запись испорчена — теряем подробности одной строки, а не страницу.
            return [];
        }
    }
}
