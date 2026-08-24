using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Feedback.Models;
using delosfera_server.Modules.Feedback.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Feedback.Controllers;

public class VisitRecord
{
    /// <summary>Адрес экрана. Приводится к шаблону маршрута на сервере.</summary>
    public string? Path { get; set; }

    public string? Title { get; set; }

    /// <summary>Момент захода по часам браузера — сервер их проверяет и при расхождении не берёт.</summary>
    public DateTime? At { get; set; }

    /// <summary>Сколько пробыл на экране, миллисекунд.</summary>
    public int? DurationMs { get; set; }
}

public class TrackVisitsRequest
{
    public string? SessionKey { get; set; }
    public List<VisitRecord> Visits { get; set; } = [];
}

/// <summary>
/// Посещаемость экранов системы.
///
/// Отвечает на вопросы обкатки, которые иначе решаются голосованием: какие разделы
/// открывают, какие не открыл никто, кто из приглашённых к обкатке в системе так и
/// не появился. Пишет каждый за себя; читает тот, кто ведёт статистику.
///
/// Отдельно от журнала действий: переходов на порядок больше, чем действий с
/// документами, и смешивать поток, где ищут след документа, с потоком, где считают
/// заходы, — испортить оба.
/// </summary>
[ApiController]
[Authorize]
[Route("api/usage")]
[Tags("Посещаемость")]
public class UsageController : ControllerBase
{
    /// <summary>
    /// Переходов в одной посылке. Браузер копит их и шлёт пачкой — иначе каждый
    /// клик по меню стоил бы отдельного обращения к серверу, а скорость работы мы
    /// только что чинили.
    /// </summary>
    private const int MaxBatch = 50;

    /// <summary>
    /// Насколько часы браузера могут расходиться с серверными, чтобы им ещё верить.
    /// Дальше берём серверное время: отчёт с заходами из завтрашнего дня бесполезен.
    /// </summary>
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(12);

    /// <summary>Сутки на экране — заведомо забытая вкладка, а не работа.</summary>
    private const int MaxDurationMs = 24 * 60 * 60 * 1000;

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UsageController(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Принять пачку переходов от браузера.</summary>
    [HttpPost("visits")]
    public async Task<IActionResult> Track([FromBody] TrackVisitsRequest request, CancellationToken ct)
    {
        if (request.Visits.Count == 0)
            return Ok(new { accepted = 0 });

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        var sessionKey = Trim(request.SessionKey, 64);

        var rows = new List<PageVisit>(Math.Min(request.Visits.Count, MaxBatch));

        foreach (var visit in request.Visits.Take(MaxBatch))
        {
            var (routePath, entityId) = RoutePathNormalizer.Normalize(visit.Path);

            var at = visit.At.HasValue
                ? visit.At.Value.ToUniversalTime()
                : now;

            // Часам браузера верим, пока они правдоподобны: они дают верный порядок
            // переходов внутри сессии. Но сбитые часы не должны попадать в отчёт.
            if (at > now.Add(ClockTolerance) || at < now.Subtract(ClockTolerance))
                at = now;

            rows.Add(new PageVisit
            {
                UserId = userId,
                RoutePath = routePath,
                EntityId = entityId,
                Title = Trim(visit.Title, 200),
                VisitedAt = at,
                DurationMs = visit.DurationMs is > 0 and <= MaxDurationMs ? visit.DurationMs : null,
                SessionKey = sessionKey,
            });
        }

        _db.PageVisits.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        return Ok(new { accepted = rows.Count });
    }

    /// <summary>
    /// Отчёт целиком: показатели, дни, разделы и поимённый список сотрудников.
    ///
    /// Одним запросом, а не шестью: страница показывает всё сразу, и шесть
    /// обращений подряд означали бы шесть разных мгновений — цифры в шапке
    /// расходились бы с таблицей под ней.
    ///
    /// В список входят и те, кто не заходил ни разу. Они и есть главный вопрос
    /// к отчёту: молчание подразделения читают как «замечаний нет», а обычно
    /// это «мы не начинали».
    /// </summary>
    [HttpGet("report")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Report([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var (from, to) = Period(days);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAgo = today.AddDays(-7);

        var visits = _db.PageVisits.AsNoTracking().Where(v => v.VisitedAt >= from && v.VisitedAt < to);

        // Показатели шапки
        var totalOpens = await visits.CountAsync(ct);
        var reached = await visits.Select(v => v.UserId).Distinct().CountAsync(ct);

        var enabled = await _db.Users.CountAsync(u => u.IsActive && u.BlockedAt == null, ct);

        var todayCount = await _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Select(v => v.UserId).Distinct().CountAsync(ct);

        var weekCount = await _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= weekAgo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Select(v => v.UserId).Distinct().CountAsync(ct);

        // Ни разу не заходили — за всё время, а не за период: человек, зашедший
        // однажды в марте, уже не «ни разу», даже если в этом месяце его не было.
        var everVisited = await _db.PageVisits.AsNoTracking()
            .Select(v => v.UserId).Distinct().CountAsync(ct);
        var neverVisited = Math.Max(enabled - everVisited, 0);

        // По дням
        var byDay = await visits
            .GroupBy(v => v.VisitedAt.Date)
            .Select(g => new { Day = g.Key, Users = g.Select(v => v.UserId).Distinct().Count() })
            .OrderBy(x => x.Day)
            .ToListAsync(ct);

        // По разделам: сводим маршруты в разделы уже в памяти — сопоставление
        // живёт в коде, а не в базе, и переносить его в запрос незачем.
        var byRoute = await visits
            .GroupBy(v => v.RoutePath)
            .Select(g => new { Route = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var sections = byRoute
            .GroupBy(r => SectionMap.Title(r.Route))
            .Select(g => new { title = g.Key, count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.count)
            .Take(15)
            .ToList();

        // Поимённо: сначала те, кто заходил, потом молчавшие.
        var active = await visits
            .GroupBy(v => v.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Opens = g.Count(),
                Days = g.Select(v => v.VisitedAt.Date).Distinct().Count(),
                Last = g.Max(v => v.VisitedAt),
            })
            .ToListAsync(ct);

        var stats = active.ToDictionary(a => a.UserId);

        var people = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.BlockedAt == null)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                OrgUnit = u.OrgUnit == null ? null : u.OrgUnit.TitleRu,
            })
            .ToListAsync(ct);

        var employees = people
            .Select(p =>
            {
                stats.TryGetValue(p.Id, out var s);
                return new
                {
                    userId = p.Id,
                    fullName = p.FullName,
                    position = p.Position,
                    orgUnit = p.OrgUnit,
                    days = s?.Days ?? 0,
                    opens = s?.Opens ?? 0,
                    lastVisit = s?.Last,
                };
            })
            .OrderByDescending(e => e.opens).ThenBy(e => e.fullName)
            .ToList();

        return Ok(new
        {
            days,
            from,
            to,
            summary = new
            {
                reached,
                enabled,
                // Доля справочника: сколько сотрудников из заведённых вообще
                // пользуются системой. Это и есть ответ на «внедрилось ли».
                share = enabled == 0 ? 0 : (int)Math.Round(reached * 100.0 / enabled),
                totalOpens,
                today = todayCount,
                week = weekCount,
                neverVisited,
            },
            byDay,
            sections,
            employees,
        });
    }

    /// <summary>Тот же отчёт таблицей — для разбора в Excel.</summary>
    [HttpGet("report.csv")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> ReportCsv([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var (from, to) = Period(days);

        var active = await _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= from && v.VisitedAt < to)
            .GroupBy(v => v.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Opens = g.Count(),
                Days = g.Select(v => v.VisitedAt.Date).Distinct().Count(),
                Last = g.Max(v => v.VisitedAt),
            })
            .ToListAsync(ct);

        var stats = active.ToDictionary(a => a.UserId);

        var people = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.BlockedAt == null)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                OrgUnit = u.OrgUnit == null ? null : u.OrgUnit.TitleRu,
            })
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var text = new System.Text.StringBuilder();

        // Разделитель — точка с запятой: Excel с русскими настройками разбирает
        // запятую как десятичный знак и складывает всю строку в одну ячейку.
        text.AppendLine("Сотрудник;Должность;Подразделение;Дней с посещениями;Открытий;Последний вход");

        foreach (var person in people)
        {
            stats.TryGetValue(person.Id, out var s);

            text.AppendLine(string.Join(';',
                Csv(person.FullName),
                Csv(person.Position),
                Csv(person.OrgUnit),
                s?.Days ?? 0,
                s?.Opens ?? 0,
                s is null ? "" : s.Last.ToString("dd.MM.yyyy HH:mm")));
        }

        // Метка порядка байтов: без неё Excel открывает кириллицу как «ЗаголовокЛ».
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(text.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"посещения-{days}дн.csv");
    }

    /// <summary>Экранирует значение для CSV: кавычки удваиваются, разделители прячутся.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>Общая картина за период: сколько заходов, кто был, что открывали.</summary>
    [HttpGet("overview")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Overview([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var (from, to) = Period(days);
        var visits = _db.PageVisits.AsNoTracking().Where(v => v.VisitedAt >= from && v.VisitedAt < to);

        var total = await visits.CountAsync(ct);
        var people = await visits.Select(v => v.UserId).Distinct().CountAsync(ct);
        var screens = await visits.Select(v => v.RoutePath).Distinct().CountAsync(ct);

        // Сколько всего людей могли зайти — чтобы «был 21 человек» читалось как доля.
        var enabled = await _db.Users.CountAsync(u => u.IsActive && u.BlockedAt == null, ct);

        var median = await MedianDuration(visits, ct);

        return Ok(new
        {
            days,
            from,
            to,
            totalVisits = total,
            distinctUsers = people,
            distinctScreens = screens,
            enabledUsers = enabled,
            medianDurationMs = median,
        });
    }

    /// <summary>Экраны по числу заходов: что открывают, а что не открыл никто.</summary>
    [HttpGet("pages")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Pages([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var (from, to) = Period(days);

        var pages = await _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= from && v.VisitedAt < to)
            .GroupBy(v => v.RoutePath)
            .Select(g => new
            {
                RoutePath = g.Key,
                Title = g.Max(v => v.Title),
                Visits = g.Count(),
                Users = g.Select(v => v.UserId).Distinct().Count(),
                AverageDurationMs = (int?)g.Where(v => v.DurationMs != null).Average(v => v.DurationMs),
                LastAt = g.Max(v => v.VisitedAt),
            })
            .OrderByDescending(x => x.Visits)
            .Take(100)
            .ToListAsync(ct);

        return Ok(pages);
    }

    /// <summary>Люди по активности: кто работает в системе, а кто ещё не заходил.</summary>
    [HttpGet("users")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Users([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var (from, to) = Period(days);

        var active = await _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= from && v.VisitedAt < to)
            .GroupBy(v => v.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Visits = g.Count(),
                Screens = g.Select(v => v.RoutePath).Distinct().Count(),
                Days = g.Select(v => v.VisitedAt.Date).Distinct().Count(),
                FirstAt = g.Min(v => v.VisitedAt),
                LastAt = g.Max(v => v.VisitedAt),
            })
            .OrderByDescending(x => x.Visits)
            .Take(200)
            .ToListAsync(ct);

        var ids = active.Select(a => a.UserId).ToList();

        var people = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                OrgUnit = u.OrgUnit == null ? null : u.OrgUnit.TitleRu,
            })
            .ToDictionaryAsync(u => u.Id, ct);

        var rows = active.Select(a => new
        {
            a.UserId,
            FullName = people.TryGetValue(a.UserId, out var p) ? p.FullName : "— удалён —",
            Position = people.TryGetValue(a.UserId, out var p2) ? p2.Position : null,
            OrgUnit = people.TryGetValue(a.UserId, out var p3) ? p3.OrgUnit : null,
            a.Visits,
            a.Screens,
            a.Days,
            a.FirstAt,
            a.LastAt,
        });

        return Ok(rows);
    }

    /// <summary>
    /// Кто ни разу не заходил за период. Для обкатки это главный список: молчание
    /// подразделения читают как «замечаний нет», а обычно это «мы не начинали».
    /// </summary>
    [HttpGet("silent")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Silent([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var (from, to) = Period(days);

        var visited = _db.PageVisits.AsNoTracking()
            .Where(v => v.VisitedAt >= from && v.VisitedAt < to)
            .Select(v => v.UserId);

        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.BlockedAt == null && !visited.Contains(u.Id))
            .OrderBy(u => u.OrgUnit!.TitleRu).ThenBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                OrgUnit = u.OrgUnit == null ? null : u.OrgUnit.TitleRu,
                u.LastLoginAt,
            })
            .Take(600)
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>Заходы по дням и по часам — когда системой пользуются.</summary>
    [HttpGet("timeline")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> Timeline([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var (from, to) = Period(days);
        var visits = _db.PageVisits.AsNoTracking().Where(v => v.VisitedAt >= from && v.VisitedAt < to);

        var byDay = await visits
            .GroupBy(v => v.VisitedAt.Date)
            .Select(g => new
            {
                Day = g.Key,
                Visits = g.Count(),
                Users = g.Select(v => v.UserId).Distinct().Count(),
            })
            .OrderBy(x => x.Day)
            .ToListAsync(ct);

        var byHour = await visits
            .GroupBy(v => v.VisitedAt.Hour)
            .Select(g => new { Hour = g.Key, Visits = g.Count() })
            .OrderBy(x => x.Hour)
            .ToListAsync(ct);

        return Ok(new { byDay, byHour });
    }

    /// <summary>Путь одного человека по экранам — последние заходы по времени.</summary>
    [HttpGet("users/{userId:int}")]
    [RequirePermission(PermissionCode.ViewFullStatistics)]
    public async Task<IActionResult> UserTrail(int userId, [FromQuery] int take = 200, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 1000);

        var rows = await _db.PageVisits.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.VisitedAt)
            .Take(take)
            .Select(v => new { v.RoutePath, v.EntityId, v.Title, v.VisitedAt, v.DurationMs })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>
    /// Медиана, а не среднее: одна забытая на ночь вкладка сдвигает среднее так,
    /// что оно перестаёт описывать хоть кого-нибудь.
    /// </summary>
    private static async Task<int?> MedianDuration(IQueryable<PageVisit> visits, CancellationToken ct)
    {
        var measured = visits.Where(v => v.DurationMs != null);
        var count = await measured.CountAsync(ct);
        if (count == 0) return null;

        return await measured
            .OrderBy(v => v.DurationMs)
            .Skip(count / 2)
            .Select(v => v.DurationMs)
            .FirstOrDefaultAsync(ct);
    }

    private static (DateTime From, DateTime To) Period(int days)
    {
        days = Math.Clamp(days, 1, 365);
        var to = DateTime.UtcNow.Date.AddDays(1);
        return (to.AddDays(-days), to);
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
