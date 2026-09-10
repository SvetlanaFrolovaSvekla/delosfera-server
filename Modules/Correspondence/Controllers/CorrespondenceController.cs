using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Correspondence.DTO;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Correspondence.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Correspondence.Controllers;

/// <summary>
/// Книга регистрации входящей и исходящей корреспонденции.
///
/// Одна книга на все категории: обычная переписка, запросы регулятора, обращения
/// клиентов, запросы по счетам. Различаются они сроками и доступом, а не
/// устройством — искать письмо в трёх разных реестрах хуже, чем фильтровать в одном.
/// </summary>
[ApiController]
[Authorize]
[Route("api/correspondence")]
[Tags("Корреспонденция")]
public class CorrespondenceController : ControllerBase
{
    private readonly ILetterService _letters;
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CorrespondenceController(
        ILetterService letters, DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _letters = letters;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Книга регистрации с фильтрами.</summary>
    [HttpPost("search")]
    [RequirePermission(PermissionCode.ViewCorrespondence)]
    public async Task<IActionResult> Search([FromBody] LetterFilterRequest filter, CancellationToken ct) =>
        Ok(await _letters.SearchAsync(filter, ct));

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCode.ViewCorrespondence)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        try { return Ok(await _letters.GetAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    /// <summary>
    /// Что просрочено. Отдельная ручка, потому что этот вопрос задают каждое утро,
    /// а не при разборе реестра.
    /// </summary>
    [HttpGet("overdue")]
    [RequirePermission(PermissionCode.ViewCorrespondence)]
    public async Task<IActionResult> Overdue(CancellationToken ct) =>
        Ok(await _letters.OverdueAsync(ct));

    /// <summary>Зарегистрировать письмо в книге.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> Register([FromBody] LetterSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _letters.RegisterAsync(request, _currentUser.UserId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> Update(int id, [FromBody] LetterSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _letters.UpdateAsync(id, request, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }

    /// <summary>Резолюция руководителя: кому, что и к какому сроку.</summary>
    [HttpPost("{id:int}/resolve")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveLetterRequest request, CancellationToken ct)
    {
        try { return Ok(await _letters.ResolveAsync(id, request, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    /// <summary>Закрыть письмо без ответа с указанием, чем закончилось.</summary>
    [HttpPost("{id:int}/close")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> Close(int id, [FromBody] CloseLetterRequest request, CancellationToken ct)
    {
        try { return Ok(await _letters.CloseAsync(id, request.Note, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    /// <summary>Отправить исходящее письмо адресату (проект/зарегистрированное → отправлено).</summary>
    [HttpPost("{id:int}/send")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> Send(int id, CancellationToken ct)
    {
        try { return Ok(await _letters.SendAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    // ── справочник корреспондентов ──────────────────────────────

    /// <summary>Корреспонденты: кому пишем и кто пишет нам.</summary>
    /// <summary>
    /// Приложить файл к письму. Без этого регистрация письма оставалась записью
    /// в книге без самого документа.
    /// </summary>
    [HttpPost("{id:int}/files")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> AddFile(int id, IFormFile file, CancellationToken ct)
    {
        try
        {
            return Ok(await _letters.AddFileAsync(id, file, _currentUser.UserId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new {message = ex.Message});
        }
    }

    /// <summary>Файлы письма.</summary>
    [HttpGet("{id:int}/files")]
    public async Task<IActionResult> Files(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _letters.FilesAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new {message = ex.Message});
        }
    }

    [HttpGet("correspondents")]
    public async Task<IActionResult> Correspondents(
        [FromQuery] string? text, [FromQuery] CorrespondentKind? kind, CancellationToken ct = default)
    {
        var query = _db.Correspondents.AsNoTracking().Where(c => c.IsActive);

        if (kind is {} k) query = query.Where(c => c.Kind == k);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var needle = text.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{needle}%")
                || (c.ShortTitle != null && EF.Functions.ILike(c.ShortTitle, $"%{needle}%"))
                || (c.TaxId != null && EF.Functions.ILike(c.TaxId, $"%{needle}%")));
        }

        var rows = await query
            .OrderBy(c => c.Title)
            .Take(200)
            .Select(c => new
            {
                c.Id, c.Title, c.ShortTitle,
                Kind = c.Kind.ToString(),
                c.TaxId, c.Email, c.Phone, c.ContactPerson,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("correspondents")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> CreateCorrespondent(
        [FromBody] CorrespondentSaveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new {message = "Укажите наименование корреспондента."});

        var title = request.Title.Trim();

        // Дубликат по наименованию — то, ради чего справочник и заводился: пока
        // «НБКР» и «Нацбанк» две разные записи, переписка по одному делу не собирается.
        var exists = await _db.Correspondents
            .AnyAsync(c => c.Title.ToLower() == title.ToLower(), ct);

        if (exists)
            return Conflict(new {message = "Корреспондент с таким наименованием уже заведён."});

        var now = DateTime.UtcNow;

        var correspondent = new Correspondent
        {
            Title = title,
            ShortTitle = Trim(request.ShortTitle),
            Kind = request.Kind,
            TaxId = Trim(request.TaxId),
            Address = Trim(request.Address),
            Email = Trim(request.Email),
            Phone = Trim(request.Phone),
            ContactPerson = Trim(request.ContactPerson),
            Note = Trim(request.Note),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Correspondents.Add(correspondent);
        await _db.SaveChangesAsync(ct);

        return Ok(new {id = correspondent.Id});
    }

    [HttpPut("correspondents/{id:int}")]
    [RequirePermission(PermissionCode.RegisterCorrespondence)]
    public async Task<IActionResult> UpdateCorrespondent(
        int id, [FromBody] CorrespondentSaveRequest request, CancellationToken ct)
    {
        var correspondent = await _db.Correspondents.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (correspondent is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new {message = "Укажите наименование корреспондента."});

        correspondent.Title = request.Title.Trim();
        correspondent.ShortTitle = Trim(request.ShortTitle);
        correspondent.Kind = request.Kind;
        correspondent.TaxId = Trim(request.TaxId);
        correspondent.Address = Trim(request.Address);
        correspondent.Email = Trim(request.Email);
        correspondent.Phone = Trim(request.Phone);
        correspondent.ContactPerson = Trim(request.ContactPerson);
        correspondent.Note = Trim(request.Note);
        correspondent.IsActive = request.IsActive;
        correspondent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
