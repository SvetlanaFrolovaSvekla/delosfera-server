using System.Text;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>
/// Журнал действий с отбором (Б-10).
///
/// Аудит — это доказательство: кто, что и когда сделал. Поэтому здесь нет
/// «своих» записей, как в ленте на рабочем столе (та переехала в модуль
/// ActivityLog), — виден весь журнал, и доступ закрыт правом управления
/// пользователями: журнал показывает чужие действия и годится для
/// разбирательства.
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

    /// <summary>
    /// Журнал действий с отбором (Б-10).
    ///
    /// Аудит — это доказательство: кто, что и когда сделал. Поэтому здесь нет
    /// «своих» записей, как в ленте на рабочем столе, — виден весь журнал, и
    /// доступ закрыт правом управления пользователями: журнал показывает чужие
    /// действия и годится для разбирательства.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageUsers)]
    public async Task<IActionResult> Search(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = Filtered(from, to, userId, entityType, action);
        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(e => e.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var names = await NamesAsync(rows, ct);

        return Ok(new
        {
            total,
            page,
            pageSize,
            items = rows.Select(e => new
            {
                id = e.Id,
                at = e.At,
                userId = e.UserId,
                userName = e.UserId is { } id && names.TryGetValue(id, out var fio) ? fio : null,
                entityType = e.EntityType,
                entityId = e.EntityId,
                action = e.Action,
                actionText = Action(e.Action).Text,
                payload = e.PayloadJson,
            }),
        });
    }

    /// <summary>Перечень сущностей и действий, встречающихся в журнале, — для отбора.</summary>
    [HttpGet("dictionaries")]
    [RequirePermission(PermissionCode.ManageUsers)]
    public async Task<IActionResult> Dictionaries(CancellationToken ct = default) => Ok(new
    {
        entityTypes = await _db.AuditEntries.AsNoTracking()
            .Select(e => e.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct),
        actions = await _db.AuditEntries.AsNoTracking()
            .Select(e => e.Action).Distinct().OrderBy(x => x).ToListAsync(ct),
    });

    /// <summary>
    /// Выгрузка отобранного в CSV. Журнал предъявляют проверяющим, а они работают
    /// с выгрузкой, а не с экраном.
    /// </summary>
    [HttpGet("export")]
    [RequirePermission(PermissionCode.ManageUsers)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        CancellationToken ct = default)
    {
        var rows = await Filtered(from, to, userId, entityType, action)
            .OrderByDescending(e => e.At)
            // Выгрузка не должна валить сервер: за раз отдаём столько, сколько
            // осмысленно открыть в таблице, остальное отбирается фильтром.
            .Take(50_000)
            .ToListAsync(ct);

        var names = await NamesAsync(rows, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Дата и время;Сотрудник;Объект;Идентификатор;Действие;Подробности");

        foreach (var e in rows)
        {
            var кто = e.UserId is { } id && names.TryGetValue(id, out var fio) ? fio : "система";
            csv.AppendLine(string.Join(';', new[]
            {
                e.At.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
                Csv(кто),
                Csv(e.EntityType),
                e.EntityId.ToString(),
                Csv(Action(e.Action).Text),
                Csv(e.PayloadJson ?? string.Empty),
            }));
        }

        // Excel открывает CSV в системной кодировке, если не увидит метку порядка
        // байтов, и кириллица превращается в мусор.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"Журнал действий {DateTime.Now:dd.MM.yyyy}.csv");
    }

    private IQueryable<AuditEntry> Filtered(
        DateTime? from, DateTime? to, int? userId, string? entityType, string? action)
    {
        var query = _db.AuditEntries.AsNoTracking();

        if (from is { } f) query = query.Where(e => e.At >= f.ToUniversalTime());
        if (to is { } t) query = query.Where(e => e.At <= t.ToUniversalTime());
        if (userId is { } u) query = query.Where(e => e.UserId == u);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(e => e.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(e => e.Action == action);

        return query;
    }

    private async Task<Dictionary<int, string>> NamesAsync(List<AuditEntry> rows, CancellationToken ct)
    {
        var ids = rows.Where(e => e.UserId != null).Select(e => e.UserId!.Value).Distinct().ToList();
        return ids.Count == 0
            ? []
            : await _db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    /// <summary>Экранирование поля CSV: точка с запятой и кавычки внутри значения.</summary>
    private static string Csv(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

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