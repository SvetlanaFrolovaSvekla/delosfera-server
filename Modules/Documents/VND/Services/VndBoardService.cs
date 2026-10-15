using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Dictionaries.Services;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>"Доска ВНД" — личная витрина документов текущего пользователя.</summary>
public interface IVndBoardService
{
    Task<VndBoardResponse> GetAsync(string languageCode);

    /// <summary>Поставить документ на доску: связанный с СП — вернуть, если был скрыт; любой другой —
    /// закрепить вручную. Идемпотентно.</summary>
    Task AddAsync(int vndId);

    /// <summary>Убрать документ с доски: закреплённый вручную — открепить; связанный с СП — скрыть
    /// (его можно вернуть из списка скрытых). Идемпотентно.</summary>
    Task RemoveAsync(int vndId);
}

public class VndBoardService : IVndBoardService
{
    public const string ReasonDeveloper = "developer";
    public const string ReasonExecutor = "executor";
    public const string ReasonPinned = "pinned";

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVndService _vndService;

    public VndBoardService(DelosferaDbContext db, ICurrentUserService currentUser, IVndService vndService)
    {
        _db = db;
        _currentUser = currentUser;
        _vndService = vndService;
    }

    public async Task<VndBoardResponse> GetAsync(string languageCode)
    {
        var userId = _currentUser.UserId;
        var scope = await ResolveScopeAsync(userId);

        var auto = await LoadAutoRelatedAsync(scope.UnitIds);

        var manual = await _db.VndBoardItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.VndId, x.Mode, x.CreatedAt })
            .ToListAsync();

        var hiddenIds = manual.Where(m => m.Mode == VndBoardItemMode.Hidden).Select(m => m.VndId).ToHashSet();
        var pinned = manual.Where(m => m.Mode == VndBoardItemMode.Pinned).ToDictionary(m => m.VndId, m => m.CreatedAt);

        var reasons = new Dictionary<int, List<string>>();
        foreach (var (vndId, relation) in auto)
        {
            if (hiddenIds.Contains(vndId)) continue;
            var list = new List<string>();
            if (relation.IsDeveloper) list.Add(ReasonDeveloper);
            if (relation.IsExecutor) list.Add(ReasonExecutor);
            reasons[vndId] = list;
        }
        foreach (var vndId in pinned.Keys)
        {
            if (!reasons.TryGetValue(vndId, out var list)) reasons[vndId] = list = [];
            list.Add(ReasonPinned);
        }

        // Скрытыми показываем только то, что по-прежнему связано с СП: если связь пропала (сменили
        // разработчика / пользователь перешёл в другое СП), документ и так не появился бы на доске.
        var visibleHiddenIds = hiddenIds.Where(auto.ContainsKey).ToList();

        // Карточки документов — через обычный поиск реестра: так на доске действуют ровно те же
        // правила видимости, что и в реестре (черновики, "ещё не действующие", секретность).
        var allIds = reasons.Keys.Concat(visibleHiddenIds).Distinct().ToList();
        var documents = allIds.Count == 0
            ? []
            : await _vndService.SearchAsync(new VndSearchRequest { Ids = allIds }, languageCode);
        var byId = documents.ToDictionary(d => d.Id);

        var favorites = await _vndService.SearchAsync(new VndSearchRequest { FavoritesOnly = true }, languageCode);

        return new VndBoardResponse
        {
            OwnUnit = scope.OwnUnit is null ? null : ToUnit(scope.OwnUnit, languageCode),
            ScopeUnit = scope.ScopeUnit is null ? null : ToUnit(scope.ScopeUnit, languageCode),
            ScopeUnitCount = scope.UnitIds.Count,
            Items = reasons
                .Where(r => byId.ContainsKey(r.Key))
                .Select(r => new VndBoardEntryResponse
                {
                    Vnd = byId[r.Key],
                    Reasons = r.Value,
                    PinnedAt = pinned.TryGetValue(r.Key, out var at) ? at : null,
                })
                .OrderByDescending(e => e.Vnd.UpdatedAt)
                .ToList(),
            Hidden = visibleHiddenIds
                .Where(byId.ContainsKey)
                .Select(id => byId[id])
                .OrderBy(d => d.Code)
                .ToList(),
            Favorites = favorites.OrderByDescending(d => d.UpdatedAt).ToList(),
        };
    }

    public async Task AddAsync(int vndId)
    {
        var userId = _currentUser.UserId;
        if (!await _db.VndDocuments.AnyAsync(x => x.Id == vndId))
            throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var scope = await ResolveScopeAsync(userId);
        var isAuto = (await LoadAutoRelatedAsync(scope.UnitIds, vndId)).ContainsKey(vndId);

        await _db.VndBoardItems.Where(x => x.UserId == userId && x.VndId == vndId).ExecuteDeleteAsync();
        if (isAuto) return; // связанный с СП документ и так на доске — достаточно снять скрытие

        await SaveModeAsync(userId, vndId, VndBoardItemMode.Pinned);
    }

    public async Task RemoveAsync(int vndId)
    {
        var userId = _currentUser.UserId;
        var scope = await ResolveScopeAsync(userId);
        var isAuto = (await LoadAutoRelatedAsync(scope.UnitIds, vndId)).ContainsKey(vndId);

        await _db.VndBoardItems.Where(x => x.UserId == userId && x.VndId == vndId).ExecuteDeleteAsync();
        if (!isAuto) return; // закреплённый вручную — просто открепили

        await SaveModeAsync(userId, vndId, VndBoardItemMode.Hidden);
    }

    private async Task SaveModeAsync(int userId, int vndId, VndBoardItemMode mode)
    {
        _db.VndBoardItems.Add(new VndBoardItem
        {
            UserId = userId, VndId = vndId, Mode = mode, CreatedAt = DateTime.UtcNow,
        });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Двойной клик / две вкладки: строку уже записал параллельный запрос.
        }
    }

    private readonly record struct AutoRelation(bool IsDeveloper, bool IsExecutor);

    /// <summary>ВНД, связанные с подразделениями unitIds: СП — разработчик или ответственный
    /// исполнитель. Реквизиты берутся с текущей редакции (источник правды, см. пометку о переходном
    /// периоде в VndDocument), а у документа без редакций — с самого документа.</summary>
    private async Task<Dictionary<int, AutoRelation>> LoadAutoRelatedAsync(HashSet<int> unitIds, int? onlyVndId = null)
    {
        if (unitIds.Count == 0) return new Dictionary<int, AutoRelation>();
        var ids = unitIds.ToList(); // List.Contains гарантированно транслируется в SQL (= ANY)

        var query = _db.VndDocuments.AsNoTracking();
        if (onlyVndId is not null) query = query.Where(x => x.Id == onlyVndId);

        var rows = await query
            .Select(x => new
            {
                x.Id,
                IsDeveloper = x.CurrentRedaction != null
                    ? ids.Contains(x.CurrentRedaction.DeveloperId)
                    : ids.Contains(x.DeveloperId),
                IsExecutor = x.CurrentRedaction != null
                    ? x.CurrentRedaction.ResponsibleExecutors.Any(e => ids.Contains(e.Id))
                    : x.ResponsibleExecutors.Any(e => ids.Contains(e.Id)),
            })
            .Where(x => x.IsDeveloper || x.IsExecutor)
            .ToListAsync();

        return rows.ToDictionary(r => r.Id, r => new AutoRelation(r.IsDeveloper, r.IsExecutor));
    }

    private sealed record BoardScope(OrganizationUnit? OwnUnit, OrganizationUnit? ScopeUnit, HashSet<int> UnitIds);

    /// <summary>
    /// "СП пользователя" для доски. Сотрудники числятся в отделах и секторах, а разработчиком ВНД
    /// обычно указывают управление или департамент целиком — поэтому поднимаемся от подразделения
    /// пользователя до ближайшего управления (OrgUnitKind.Division) и берём его вместе со всеми
    /// вложенными подразделениями (OrgUnitSubtree). Выше управления не поднимаемся: иначе на доску
    /// попал бы весь банк. Если управления в цепочке нет (вид подразделений не заполнен) — берём
    /// подразделение пользователя с вложенными.
    /// </summary>
    private async Task<BoardScope> ResolveScopeAsync(int userId)
    {
        var ownUnitId = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.OrgUnitId)
            .FirstOrDefaultAsync();
        if (ownUnitId is null) return new BoardScope(null, null, []);

        var units = await _db.OrganizationUnits.AsNoTracking().ToDictionaryAsync(u => u.Id);
        if (!units.TryGetValue(ownUnitId.Value, out var ownUnit)) return new BoardScope(null, null, []);

        var scopeUnit = ownUnit;
        var visited = new HashSet<int>();
        for (OrganizationUnit? current = ownUnit; current is not null && visited.Add(current.Id);)
        {
            if (current.Kind == OrgUnitKind.Division)
            {
                scopeUnit = current;
                break;
            }
            if (current.Kind == OrgUnitKind.Board) break; // коллегиальный орган — выше не идём
            current = current.ParentId is { } parentId ? units.GetValueOrDefault(parentId) : null;
        }

        var unitIds = await OrgUnitSubtree.CollectAsync(_db.OrganizationUnits, scopeUnit.Id);
        return new BoardScope(ownUnit, scopeUnit, unitIds);
    }

    private static VndBoardUnitResponse ToUnit(OrganizationUnit unit, string languageCode) => new()
    {
        Id = unit.Id,
        Name = unit.ResolveTitle(languageCode),
    };
}
