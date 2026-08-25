using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Integrations.Controllers;

public record OrgTreeNode(
    int Id,
    string Title,
    /// <summary>Коллегиальный орган, управление или отдел. Пусто — вид не указан.</summary>
    string? Kind,
    int? ParentId,
    /// <summary>Начальник подразделения.</summary>
    string? Head,
    /// <summary>Куратор: подразделение подчинено человеку напрямую.</summary>
    string? Curator,
    /// <summary>Сколько людей числится в подразделении. Без подчинённых узлов.</summary>
    int StaffCount,
    /// <summary>Пришло из портала. Заведённые руками сюда не попадают.</summary>
    bool FromPortal,
    List<OrgTreeNode> Children);

public record OrgTreeResponse(
    List<OrgTreeNode> Roots,
    int UnitsTotal,
    /// <summary>Подразделения, у которых вышестоящее не проставлено. Видны отдельно.</summary>
    int Orphans,
    DateTime? LastSyncAt);

/// <summary>
/// Дерево подразделений для экрана оргструктуры.
///
/// Отдаётся собранным, а не плоским списком: собирать иерархию на клиенте
/// пришлось бы в каждом экране заново, и в каждом одинаково ошибаться на
/// подразделениях без родителя.
/// </summary>
[ApiController]
[Route("api/org-tree")]
[Authorize]
public class OrgTreeController(DelosferaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrgTreeResponse>> Get(CancellationToken ct)
    {
        var units = await db.OrganizationUnits
            .AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.TitleRu,
                u.ParentId,
                u.Kind,
                u.ExternalId,
                Head = u.HeadUser == null ? null : u.HeadUser.FullName,
                Curator = u.CuratorUser == null ? null : u.CuratorUser.FullName,
                StaffCount = db.Users.Count(x => x.OrgUnitId == u.Id && x.IsActive),
            })
            .ToListAsync(ct);

        var nodes = units.ToDictionary(
            u => u.Id,
            u => new OrgTreeNode(u.Id, u.TitleRu, ВидНазванием(u.Kind), u.ParentId, u.Head, u.Curator,
                                 u.StaffCount, u.ExternalId is not null, []));

        var roots = new List<OrgTreeNode>();
        var orphans = 0;

        foreach (var unit in units)
        {
            var node = nodes[unit.Id];

            if (unit.ParentId is int parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
                continue;
            }

            // Родителя нет вовсе — узел верхнего уровня. Родитель указан, но не
            // найден — подразделение осталось без места: показываем там же,
            // но считаем отдельно, чтобы это было видно числом.
            if (unit.ParentId is not null) orphans++;
            roots.Add(node);
        }

        Sort(roots);

        return Ok(new OrgTreeResponse(roots, units.Count, orphans, await LastSyncAsync(ct)));
    }

    /// <summary>
    /// Вид словом. Переводим на сервере: перечисление живёт здесь, и держать
    /// его второй копией на клиенте значило бы однажды их разойтись.
    /// </summary>
    private static string? ВидНазванием(OrgUnitKind kind) => kind switch
    {
        OrgUnitKind.Board => "Коллегиальный орган",
        OrgUnitKind.Division => "Управление",
        OrgUnitKind.Department => "Отдел",
        _ => null,
    };

    /// <summary>По алфавиту на каждом уровне: порядок в справочнике не задан, а список читают глазами.</summary>
    private static void Sort(List<OrgTreeNode> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture));
        foreach (var node in nodes) Sort(node.Children);
    }

    private async Task<DateTime?> LastSyncAsync(CancellationToken ct) =>
        await db.OrgSyncRuns
            .AsNoTracking()
            .Where(r => r.Outcome != OrgStructure.OrgSyncOutcome.Failed)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (DateTime?)r.StartedAt)
            .FirstOrDefaultAsync(ct);
}
