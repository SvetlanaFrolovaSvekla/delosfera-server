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

public class CreateFeedbackRequest
{
    public FeedbackKind Kind { get; set; } = FeedbackKind.Wish;

    /// <summary>Текст сообщения.</summary>
    public string Text { get; set; } = "";

    /// <summary>Адрес страницы, с которой пишут. Приводится к шаблону на сервере.</summary>
    public string? Path { get; set; }

    public string? PageTitle { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
}

public class HandleFeedbackRequest
{
    public FeedbackStatus Status { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Пожелания и замечания сотрудников с экранов системы.
///
/// Писать может каждый — на то и обкатка; разбирает тот, кто отвечает за настройки.
/// Свои сообщения человек видит всегда, чужие — только разбирающий: обкатка идёт
/// в подразделениях, и «у них там ничего не работает» из соседнего отдела мешает
/// сильнее, чем помогает.
/// </summary>
[ApiController]
[Authorize]
[Route("api/feedback")]
[Tags("Пожелания и замечания")]
public class FeedbackController : ControllerBase
{
    /// <summary>
    /// Столько сообщений с одного человека за час. Не от недоверия: залипшая кнопка
    /// или перезапуск вкладки в цикле способны залить таблицу за минуту.
    /// </summary>
    private const int MaxPerHour = 30;

    private const int MaxTextLength = 4000;

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FeedbackController(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Оставить сообщение с текущего экрана.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackRequest request, CancellationToken ct)
    {
        var text = (request.Text ?? "").Trim();

        if (text.Length < 3)
            return BadRequest(new { message = "Напишите, что не так или чего не хватает." });

        if (text.Length > MaxTextLength)
            text = text[..MaxTextLength];

        var userId = _currentUser.UserId;
        var hourAgo = DateTime.UtcNow.AddHours(-1);

        var recent = await _db.FeedbackItems
            .CountAsync(f => f.UserId == userId && f.CreatedAt >= hourAgo, ct);

        if (recent >= MaxPerHour)
            return StatusCode(429, new { message = "Слишком много сообщений подряд. Попробуйте позже." });

        var (routePath, entityId) = RoutePathNormalizer.Normalize(request.Path);

        var item = new FeedbackItem
        {
            Kind = request.Kind,
            Text = text,
            RoutePath = routePath,
            EntityId = entityId,
            PageTitle = Trim(request.PageTitle, 200),
            UserId = userId,
            UserAgent = Trim(Request.Headers.UserAgent.ToString(), 400),
            ViewportWidth = Sane(request.ViewportWidth),
            ViewportHeight = Sane(request.ViewportHeight),
            CreatedAt = DateTime.UtcNow,
            Status = FeedbackStatus.New,
        };

        _db.FeedbackItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = item.Id });
    }

    /// <summary>Мои сообщения и что с ними стало.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        var items = await _db.FeedbackItems
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(50)
            .Select(f => new
            {
                f.Id,
                Kind = f.Kind.ToString(),
                f.Text,
                f.RoutePath,
                f.PageTitle,
                f.CreatedAt,
                Status = f.Status.ToString(),
                f.HandlerComment,
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>Журнал сообщений для разбирающего.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Index(
        [FromQuery] FeedbackStatus? status,
        [FromQuery] FeedbackKind? kind,
        [FromQuery] string? routePath,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        var query = _db.FeedbackItems.AsNoTracking();

        if (status.HasValue) query = query.Where(f => f.Status == status.Value);
        if (kind.HasValue) query = query.Where(f => f.Kind == kind.Value);
        if (!string.IsNullOrWhiteSpace(routePath)) query = query.Where(f => f.RoutePath == routePath);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                Kind = f.Kind.ToString(),
                f.Text,
                f.RoutePath,
                f.EntityId,
                f.PageTitle,
                f.CreatedAt,
                Status = f.Status.ToString(),
                f.HandlerComment,
                f.HandledAt,
                Author = f.User == null ? null : new
                {
                    f.User.Id,
                    f.User.FullName,
                    Position = f.User.Position == null ? null : f.User.Position.TitleRu,
                    OrgUnit = f.User.OrgUnit == null ? null : f.User.OrgUnit.TitleRu,
                },
                HandledBy = f.HandledByUser == null ? null : f.HandledByUser.FullName,
                f.UserAgent,
                f.ViewportWidth,
                f.ViewportHeight,
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Сводка: сколько чего и по каким экранам пишут чаще всего.</summary>
    [HttpGet("summary")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var byStatus = await _db.FeedbackItems.AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var byKind = await _db.FeedbackItems.AsNoTracking()
            .GroupBy(f => f.Kind)
            .Select(g => new { Kind = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var byPage = await _db.FeedbackItems.AsNoTracking()
            .GroupBy(f => new { f.RoutePath, f.PageTitle })
            .Select(g => new
            {
                g.Key.RoutePath,
                g.Key.PageTitle,
                Count = g.Count(),
                Problems = g.Count(f => f.Kind == FeedbackKind.Problem),
            })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(ct);

        return Ok(new { byStatus, byKind, byPage });
    }

    /// <summary>Разобрать сообщение: сменить состояние и ответить автору.</summary>
    [HttpPatch("{id:long}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Handle(long id, [FromBody] HandleFeedbackRequest request, CancellationToken ct)
    {
        var item = await _db.FeedbackItems.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (item is null) return NotFound();

        item.Status = request.Status;
        item.HandlerComment = Trim(request.Comment, 2000);
        item.HandledByUserId = _currentUser.UserId;
        item.HandledAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    /// <summary>Отсекает заведомо невозможные размеры окна — они бы только сбивали с толку.</summary>
    private static int? Sane(int? value) =>
        value is > 0 and < 20000 ? value : null;
}
