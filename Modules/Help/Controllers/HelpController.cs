using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Help.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Help.Controllers;

public class HelpArticleRequest
{
    public HelpSection Section { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleKg { get; set; }
    public string? SummaryRu { get; set; }
    public string? SummaryKg { get; set; }

    /// <summary>Тело статьи списком блоков — приходит уже как JSON-массив.</summary>
    public JsonElement Body { get; set; }

    public string? RoutePath { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
}

/// <summary>
/// Инструкции по работе с системой (KB-01..03).
///
/// Читают все — на то они и инструкции; правит тот, кто управляет настройками.
/// Черновики видит только он же: половина статьи хуже, чем её отсутствие, потому
/// что человек уйдёт, решив, что раздел пуст.
/// </summary>
[ApiController]
[Authorize]
[Route("api/help")]
[Tags("Инструкции")]
public class HelpController : ControllerBase
{
    /// <summary>
    /// Блоков в одной статье. Ограничение не от бедности: инструкция длиннее
    /// двух десятков блоков не читается, её надо делить на статьи.
    /// </summary>
    private const int MaxBlocks = 40;

    private static readonly string[] AllowedKinds =
    [
        HelpBlockKind.Text, HelpBlockKind.Steps, HelpBlockKind.Note,
        HelpBlockKind.Link, HelpBlockKind.Vnd, HelpBlockKind.Image, HelpBlockKind.File,
    ];

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly Files.Services.IFileStorageService _files;

    public HelpController(
        DelosferaDbContext db,
        IAuditService audit,
        ICurrentUserService currentUser,
        Files.Services.IFileStorageService files)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
        _files = files;
    }

    /// <summary>Оглавление: разделы со статьями. Черновики — только редактору.</summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] bool includeDrafts = false, CancellationToken ct = default)
    {
        var mayEdit = _currentUser.HasPermission(PermissionCode.ManageSystemSettings);
        var showDrafts = includeDrafts && mayEdit;

        var articles = await _db.HelpArticles
            .AsNoTracking()
            .Where(a => showDrafts || a.IsPublished)
            .OrderBy(a => a.Section)
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.TitleRu)
            .Select(a => new
            {
                a.Id,
                section = a.Section.ToString(),
                a.TitleRu,
                a.TitleKg,
                a.SummaryRu,
                a.SummaryKg,
                a.RoutePath,
                a.IsPublished,
                a.UpdatedAt,
            })
            .ToListAsync(ct);

        return Ok(new {articles, mayEdit});
    }

    /// <summary>Статья целиком, с разобранным телом.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var article = await _db.HelpArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return NotFound(new {message = "Статья не найдена"});

        if (!article.IsPublished && !_currentUser.HasPermission(PermissionCode.ManageSystemSettings))
            return NotFound(new {message = "Статья не найдена"});

        return Ok(Shape(article));
    }

    /// <summary>
    /// Статья для конкретного экрана. По ней у страницы появляется кнопка справки,
    /// ведущая прямо к нужному тексту, а не в общее оглавление.
    /// </summary>
    [HttpGet("for-route")]
    public async Task<IActionResult> ForRoute([FromQuery] string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return Ok(new {found = false});

        var article = await _db.HelpArticles
            .AsNoTracking()
            .Where(a => a.IsPublished && a.RoutePath == path)
            .OrderBy(a => a.SortOrder)
            .FirstOrDefaultAsync(ct);

        return article is null
            ? Ok(new {found = false})
            : Ok(new {found = true, article = Shape(article)});
    }

    [HttpPost]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Create([FromBody] HelpArticleRequest request, CancellationToken ct)
    {
        if (Validate(request) is {} error) return BadRequest(new {message = error});

        var article = new HelpArticle
        {
            Section = request.Section,
            TitleRu = request.TitleRu.Trim(),
            TitleKg = Trim(request.TitleKg),
            SummaryRu = Trim(request.SummaryRu),
            SummaryKg = Trim(request.SummaryKg),
            BodyJson = request.Body.ValueKind == JsonValueKind.Undefined ? "[]" : request.Body.GetRawText(),
            RoutePath = Trim(request.RoutePath),
            SortOrder = request.SortOrder,
            IsPublished = request.IsPublished,
            UpdatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.HelpArticles.Add(article);
        await _db.SaveChangesAsync(ct);

        await SyncAttachedFilesAsync(article, ct);

        await _audit.LogAsync("HelpArticle", article.Id, "Created", _currentUser.UserId,
            new {article.TitleRu, section = article.Section.ToString(), article.IsPublished});

        return Ok(new {article.Id});
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update(int id, [FromBody] HelpArticleRequest request, CancellationToken ct)
    {
        var article = await _db.HelpArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return NotFound(new {message = "Статья не найдена"});

        if (Validate(request) is {} error) return BadRequest(new {message = error});

        var былаОпубликована = article.IsPublished;

        article.Section = request.Section;
        article.TitleRu = request.TitleRu.Trim();
        article.TitleKg = Trim(request.TitleKg);
        article.SummaryRu = Trim(request.SummaryRu);
        article.SummaryKg = Trim(request.SummaryKg);
        article.BodyJson = request.Body.ValueKind == JsonValueKind.Undefined ? "[]" : request.Body.GetRawText();
        article.RoutePath = Trim(request.RoutePath);
        article.SortOrder = request.SortOrder;
        article.IsPublished = request.IsPublished;
        article.UpdatedByUserId = _currentUser.UserId;
        article.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await SyncAttachedFilesAsync(article, ct);

        await _audit.LogAsync("HelpArticle", article.Id, "Updated", _currentUser.UserId, new
        {
            article.TitleRu,
            публикация = былаОпубликована == article.IsPublished
                ? "без изменений"
                : article.IsPublished ? "опубликована" : "снята с публикации",
        });

        return Ok(new {article.Id, article.UpdatedAt});
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var article = await _db.HelpArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return NotFound(new {message = "Статья не найдена"});

        _db.HelpArticles.Remove(article);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("HelpArticle", id, "Deleted", _currentUser.UserId, new {article.TitleRu});

        return Ok(new {ok = true});
    }

    // ── снимки экрана и приложенные файлы ───────────────────────────────────

    /// <summary>Столько вложений (снимков и файлов вместе) в одной статье за один сеанс
    /// синхронизации. Больше — значит статью надо делить.</summary>
    private const int MaxAttachmentsPerArticle = 20;

    /// <summary>
    /// Загрузить снимок экрана для статьи. Отдаёт идентификатор файла — его
    /// редактор кладёт в блок изображения.
    /// </summary>
    [HttpPost("images")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new {message = "Файл пустой."});

        // Только растровые изображения. SVG исключён намеренно: это документ,
        // который может нести скрипт, и открывается он в браузере сотрудника.
        var allowed = new[] {"image/png", "image/jpeg", "image/webp", "image/gif"};
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new {message = "Допустимы только изображения PNG, JPEG, WEBP или GIF."});

        const long maxBytes = 8 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(new {message = "Снимок экрана больше 8 МБ — уменьшите его."});

        var attachment = await _files.SaveAsync(file, _currentUser.UserId, ct);
        return Ok(new {fileId = attachment.Id, fileName = attachment.OriginalFileName, size = attachment.SizeBytes});
    }

    /// <summary>
    /// Загрузить файл для статьи (например, полную инструкцию в .docx) — блок
    /// "file" отдаёт его читателю кнопками "Скачать" и "Просмотр". Отдаёт
    /// идентификатор файла — его редактор кладёт в блок.
    /// </summary>
    [HttpPost("files")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new {message = "Файл пустой."});

        var allowed = new[]
        {
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
            "application/pdf",
        };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new {message = "Допустимы файлы формата .docx или .pdf."});

        const long maxBytes = 30 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(new {message = "Файл больше 30 МБ — уменьшите его."});

        var attachment = await _files.SaveAsync(file, _currentUser.UserId, ct);
        return Ok(new {fileId = attachment.Id, fileName = attachment.OriginalFileName, size = attachment.SizeBytes});
    }

    /// <summary>
    /// Выдать снимок экрана статьи.
    ///
    /// Отдельно от общей выдачи файлов: та разрешает доступ автору вложения и
    /// участникам документа, а инструкция участников не имеет — её читают все.
    /// Здесь проверка своя: файл отдаётся, если он привязан к опубликованной
    /// статье, а неопубликованной — только тому, кто статьи правит.
    /// </summary>
    [HttpGet("images/{fileId:int}")]
    public async Task<IActionResult> GetImage(int fileId, CancellationToken ct)
    {
        var mayEdit = _currentUser.HasPermission(PermissionCode.ManageSystemSettings);

        var allowed = await _db.HelpArticleImages
            .AnyAsync(i => i.FileId == fileId && (mayEdit || i.Article!.IsPublished), ct);

        if (!allowed) return NotFound();

        var (stream, contentType, fileName) = await _files.DownloadAsync(fileId, ct);

        // Снимки интерфейса меняются вместе со сборкой, а не по часам: сутки в
        // кеше браузера снимают лишние обращения, не делая инструкцию устаревшей.
        Response.Headers.CacheControl = "private, max-age=86400";

        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Выдать файл, приложенный к статье блоком "file" (например, .docx с полной
    /// инструкцией). Проверка доступа та же, что у снимков — привязан ли файл к
    /// опубликованной статье (см. HelpArticleImages, эта же таблица связей
    /// используется и для файлов, не только изображений). В отличие от снимка
    /// отдаётся с настоящим именем файла: и для скачивания, и для просмотра через
    /// docx-preview на клиенте (см. useDocxPreview, fetchFileBlob) нужно оригинальное
    /// имя и content-type документа, а не изображения.
    /// </summary>
    [HttpGet("files/{fileId:int}")]
    public async Task<IActionResult> GetFile(int fileId, CancellationToken ct)
    {
        var mayEdit = _currentUser.HasPermission(PermissionCode.ManageSystemSettings);

        var allowed = await _db.HelpArticleImages
            .AnyAsync(i => i.FileId == fileId && (mayEdit || i.Article!.IsPublished), ct);

        if (!allowed) return NotFound();

        var (stream, contentType, fileName) = await _files.DownloadAsync(fileId, ct);
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Приводит список привязанных файлов в соответствие телу статьи: добавляет
    /// появившиеся, убирает исчезнувшие. Общая для блоков "image" и "file" — обоим
    /// нужна одна и та же запись в HelpArticleImages, чтобы GetImage/GetFile могли
    /// проверить доступ одним запросом, не разбирая JSON тела статьи.
    ///
    /// Без уборки удалённый из текста снимок или файл остался бы доступен по прямой
    /// ссылке — а его могли удалить именно потому, что он показывал лишнее.
    /// </summary>
    private async Task SyncAttachedFilesAsync(HelpArticle article, CancellationToken ct)
    {
        var referenced = ExtractAttachedFileIds(article.BodyJson);

        var existing = await _db.HelpArticleImages
            .Where(i => i.ArticleId == article.Id)
            .ToListAsync(ct);

        var stale = existing.Where(i => !referenced.Contains(i.FileId)).ToList();
        if (stale.Count > 0) _db.HelpArticleImages.RemoveRange(stale);

        var known = existing.Select(i => i.FileId).ToHashSet();

        foreach (var fileId in referenced.Where(id => !known.Contains(id)).Take(MaxAttachmentsPerArticle))
        {
            _db.HelpArticleImages.Add(new HelpArticleImage
            {
                ArticleId = article.Id,
                FileId = fileId,
                UploadedByUserId = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (stale.Count > 0 || referenced.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    /// <summary>Идентификаторы файлов из блоков изображения и файла в теле статьи.</summary>
    private static HashSet<int> ExtractAttachedFileIds(string bodyJson)
    {
        var ids = new HashSet<int>();

        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return ids;

            foreach (var block in doc.RootElement.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;

                if (!block.TryGetProperty("kind", out var kind))
                    continue;

                var kindValue = kind.GetString();
                if (kindValue != HelpBlockKind.Image && kindValue != HelpBlockKind.File)
                    continue;

                if (block.TryGetProperty("fileId", out var fileId)
                    && fileId.TryGetInt32(out var value)
                    && value > 0)
                {
                    ids.Add(value);
                }
            }
        }
        catch (JsonException)
        {
            // Тело неразборчиво — связей не трогаем. Чинить его должен тот, кто
            // испортил, а не эта уборка.
        }

        return ids;
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private object Shape(HelpArticle a) => new
    {
        a.Id,
        section = a.Section.ToString(),
        a.TitleRu,
        a.TitleKg,
        a.SummaryRu,
        a.SummaryKg,
        a.RoutePath,
        a.SortOrder,
        a.IsPublished,
        a.UpdatedAt,
        updatedByName = _db.Users.Where(u => u.Id == a.UpdatedByUserId)
            .Select(u => u.FullName).FirstOrDefault(),

        // Тело отдаём разобранным, а не строкой: клиенту иначе пришлось бы
        // разбирать её самому, и ошибка разбора ломала бы всю страницу.
        body = ParseBody(a.BodyJson),
    };

    private static JsonElement ParseBody(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Тело могло быть записано вручную запросом к базе. Терять из-за этого
            // статью целиком нельзя — покажем её пустой, заголовок останется.
            using var empty = JsonDocument.Parse("[]");
            return empty.RootElement.Clone();
        }
    }

    /// <summary>
    /// Проверка того, что автор статьи не оставит читателя с пустой страницей и не
    /// сохранит блок, который интерфейс не умеет показать.
    /// </summary>
    private static string? Validate(HelpArticleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TitleRu))
            return "У статьи должно быть название";

        if (request.Body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        if (request.Body.ValueKind != JsonValueKind.Array)
            return "Тело статьи должно быть списком блоков";

        var blocks = request.Body.EnumerateArray().ToList();

        if (blocks.Count > MaxBlocks)
            return $"В статье больше {MaxBlocks} блоков — разделите её на несколько";

        foreach (var block in blocks)
        {
            if (block.ValueKind != JsonValueKind.Object)
                return "Каждый блок статьи должен быть объектом";

            if (!block.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
                return "У каждого блока должен быть указан вид";

            if (!AllowedKinds.Contains(kind.GetString()))
                return $"Неизвестный вид блока: {kind.GetString()}";
        }

        if (request.IsPublished && blocks.Count == 0)
            return "Пустую статью публиковать нельзя — сотрудник решит, что раздел не работает";

        return null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
