using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Controllers;

public record JournalViewDto(
    int Id,
    string Journal,
    string Name,
    List<string> Columns,
    /// <summary>Общее представление — заведено для всех, а не для себя.</summary>
    bool IsShared,
    /// <summary>Для какого подразделения. Пусто — для всех.</summary>
    int? OrgUnitId,
    string? OrgUnitTitle,
    bool IsDefault,
    /// <summary>Можно ли править: своё — да, чужое общее — только с правом на настройки.</summary>
    bool CanEdit);

public record JournalViewSaveRequest(
    string Name,
    List<string> Columns,
    /// <summary>Завести общим. Требует права на системные настройки.</summary>
    bool IsShared,
    int? OrgUnitId,
    bool IsDefault);

/// <summary>
/// Представления журналов: наборы колонок в списках документов.
///
/// Читать может каждый — иначе человек не увидит даже своих. Заводить общие
/// представления может только тот, кто настраивает систему: общее видят все,
/// и заводить их каждому означало бы свалку из полусотни наборов.
/// </summary>
[ApiController]
[Route("api/journal-views")]
[Authorize]
public class JournalViewController(
    DelosferaDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Представления этого журнала: свои и общие, доступные пользователю.
    ///
    /// Общее представление с подразделением показывается только его сотрудникам:
    /// набор колонок канцелярии кредитному отделу ни о чём не говорит.
    /// </summary>
    [HttpGet("{journal}")]
    public async Task<ActionResult<IEnumerable<JournalViewDto>>> List(string journal, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var myUnitId = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.OrgUnitId)
            .FirstOrDefaultAsync(ct);

        var views = await db.JournalViews
            .AsNoTracking()
            .Include(v => v.OrgUnit)
            .Where(v => v.Journal == journal)
            .Where(v => v.OwnerUserId == userId
                        || (v.OwnerUserId == null
                            && (v.OrgUnitId == null || v.OrgUnitId == myUnitId)))
            .OrderByDescending(v => v.OwnerUserId == userId)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

        var mayManage = currentUser.HasPermission(PermissionCode.ManageSystemSettings);

        return Ok(views.Select(v => ToDto(v, userId, mayManage)));
    }

    [HttpPost("{journal}")]
    public async Task<ActionResult<JournalViewDto>> Create(
        string journal, [FromBody] JournalViewSaveRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new {message = error});

        var mayManage = currentUser.HasPermission(PermissionCode.ManageSystemSettings);
        if (request.IsShared && !mayManage)
            return BadRequest(new {message = "Общее представление может завести только администратор системы."});

        var now = DateTime.UtcNow;
        var view = new JournalView
        {
            Journal = journal,
            Name = request.Name.Trim(),
            ColumnsJson = JsonSerializer.Serialize(Clean(request.Columns), Json),
            OwnerUserId = request.IsShared ? null : currentUser.UserId,
            OrgUnitId = request.IsShared ? request.OrgUnitId : null,
            IsDefault = request.IsDefault,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.JournalViews.Add(view);
        await ClearOtherDefaultsAsync(view, ct);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(view, currentUser.UserId, mayManage));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<JournalViewDto>> Update(
        int id, [FromBody] JournalViewSaveRequest request, CancellationToken ct)
    {
        var view = await db.JournalViews.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (view is null) return NotFound();

        var mayManage = currentUser.HasPermission(PermissionCode.ManageSystemSettings);
        if (!CanEdit(view, currentUser.UserId, mayManage))
            return BadRequest(new {message = "Чужое представление изменить нельзя."});

        var error = Validate(request);
        if (error is not null) return BadRequest(new {message = error});

        view.Name = request.Name.Trim();
        view.ColumnsJson = JsonSerializer.Serialize(Clean(request.Columns), Json);
        view.IsDefault = request.IsDefault;
        if (view.OwnerUserId is null) view.OrgUnitId = request.OrgUnitId;
        view.UpdatedAt = DateTime.UtcNow;

        await ClearOtherDefaultsAsync(view, ct);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(view, currentUser.UserId, mayManage));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var view = await db.JournalViews.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (view is null) return NotFound();

        var mayManage = currentUser.HasPermission(PermissionCode.ManageSystemSettings);
        if (!CanEdit(view, currentUser.UserId, mayManage))
            return BadRequest(new {message = "Чужое представление удалить нельзя."});

        db.JournalViews.Remove(view);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private static string? Validate(JournalViewSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Дайте представлению название — по нему его выбирают.";

        if (Clean(request.Columns).Count == 0)
            return "Оставьте хотя бы одну колонку: пустой список нечего показывать.";

        return null;
    }

    /// <summary>Убирает пустое и повторы, сохраняя порядок: он и есть порядок столбцов.</summary>
    private static List<string> Clean(List<string> columns) =>
        columns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(60)
            .ToList();

    private static bool CanEdit(JournalView view, int userId, bool mayManage) =>
        view.OwnerUserId == userId || (view.OwnerUserId is null && mayManage);

    /// <summary>
    /// По умолчанию может быть только одно — своё или общее, но одно.
    /// Иначе при открытии журнала выбор между ними достаётся случаю.
    /// </summary>
    private async Task ClearOtherDefaultsAsync(JournalView view, CancellationToken ct)
    {
        if (!view.IsDefault) return;

        var siblings = await db.JournalViews
            .Where(v => v.Journal == view.Journal
                        && v.OwnerUserId == view.OwnerUserId
                        && v.Id != view.Id
                        && v.IsDefault)
            .ToListAsync(ct);

        foreach (var sibling in siblings) sibling.IsDefault = false;
    }

    private static JournalViewDto ToDto(JournalView v, int userId, bool mayManage) =>
        new(v.Id, v.Journal, v.Name,
            JsonSerializer.Deserialize<List<string>>(v.ColumnsJson) ?? [],
            v.OwnerUserId is null,
            v.OrgUnitId, v.OrgUnit?.TitleRu,
            v.IsDefault,
            CanEdit(v, userId, mayManage));
}
