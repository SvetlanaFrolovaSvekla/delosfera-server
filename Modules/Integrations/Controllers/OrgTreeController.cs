using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Integrations.Controllers;

public record OrgTreeNode(
    int Id,
    string Title,
    /// <summary>Коллегиальный орган, управление или отдел. У человека — должность.</summary>
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
    List<OrgTreeNode> Children,
    /// <summary>Узел — не подразделение, а человек, которому подчинены нижние.</summary>
    bool IsPerson = false,
    /// <summary>Кто числится в подразделении. У узла-человека пусто.</summary>
    List<OrgTreeStaff>? Staff = null);

/// <summary>Сотрудник подразделения: столько, сколько нужно строке списка.</summary>
public record OrgTreeStaff(int Id, string FullName, string? Position, bool IsHead);

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
                u.CuratorUserId,
                Head = u.HeadUser == null ? null : u.HeadUser.FullName,
                Curator = u.CuratorUser == null ? null : u.CuratorUser.FullName,
                u.HeadUserId,
                StaffCount = db.Users.Count(x => x.OrgUnitId == u.Id && x.IsActive),
            })
            .ToListAsync(ct);

        // Сотрудников берём одним запросом на всё дерево, а не по запросу на
        // раскрытие узла: подразделений полторы сотни, и сто пятьдесят обращений
        // за списком из шести человек — это сто пятьдесят обращений.
        var сотрудники = await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.OrgUnitId != null)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                Position = x.Position == null ? null : x.Position.TitleRu,
                UnitId = x.OrgUnitId!.Value,
            })
            .ToListAsync(ct);

        var поПодразделениям = сотрудники
            .GroupBy(x => x.UnitId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var nodes = units.ToDictionary(
            u => u.Id,
            u => new OrgTreeNode(u.Id, u.TitleRu, ВидНазванием(u.Kind), u.ParentId, u.Head, u.Curator,
                                 u.StaffCount, u.ExternalId is not null, [],
                                 Staff: поПодразделениям.TryGetValue(u.Id, out var люди)
                                     // Начальник первым: список читают, чтобы
                                     // понять, к кому идти, а не по алфавиту.
                                     ? люди
                                         .OrderByDescending(x => x.Id == u.HeadUserId)
                                         .ThenBy(x => x.FullName, StringComparer.CurrentCulture)
                                         .Select(x => new OrgTreeStaff(
                                             x.Id, x.FullName, x.Position, x.Id == u.HeadUserId))
                                         .ToList()
                                     : []));

        var roots = new List<OrgTreeNode>();
        var orphans = 0;
        var подчинённыеКуратору = new List<(int CuratorId, OrgTreeNode Node)>();

        foreach (var unit in units)
        {
            var node = nodes[unit.Id];

            if (unit.ParentId is int parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
                continue;
            }

            // Родителя-подразделения нет, но есть куратор — подразделение подчинено
            // человеку. Портал так и присылает: у верхних узлов вместо вышестоящего
            // подразделения стоит зампред. Место такому узлу — под этим человеком.
            if (unit.ParentId is null && unit.CuratorUserId is int curatorId)
            {
                подчинённыеКуратору.Add((curatorId, node));
                continue;
            }

            // Родителя нет вовсе — узел верхнего уровня. Родитель указан, но не
            // найден — подразделение осталось без места: показываем там же,
            // но считаем отдельно, чтобы это было видно числом.
            if (unit.ParentId is not null) orphans++;
            roots.Add(node);
        }

        await РасставитьПоКураторамАsync(подчинённыеКуратору, nodes, roots, ct);

        Sort(roots);

        return Ok(new OrgTreeResponse(roots, units.Count, orphans, await LastSyncAsync(ct)));
    }

    /// <summary>
    /// Вешает подразделения под их кураторов, а самих кураторов — под теми
    /// подразделениями, где они числятся.
    ///
    /// Так устроен банк: часть управлений подчинена не вышестоящему управлению,
    /// а заместителю Председателя. Портал это и присылает — вместо вышестоящего
    /// подразделения приходит человек. Без этого шага дерево рассыпается на
    /// десятки обрубков, где «Административный отдел» стоит вровень с «Правлением».
    /// </summary>
    private async Task РасставитьПоКураторамАsync(
        List<(int CuratorId, OrgTreeNode Node)> подчинённые,
        Dictionary<int, OrgTreeNode> nodes,
        List<OrgTreeNode> roots,
        CancellationToken ct)
    {
        if (подчинённые.Count == 0) return;

        var ids = подчинённые.Select(x => x.CuratorId).Distinct().ToList();

        var кураторы = await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position == null ? null : u.Position.TitleRu,
                u.OrgUnitId,
            })
            .ToListAsync(ct);

        var узлыЛюдей = new Dictionary<int, OrgTreeNode>();

        foreach (var куратор in кураторы)
        {
            // Идентификатор человека берём со знаком минус: клиент различает узлы
            // по нему, а подразделение с таким же номером существует всегда.
            var узел = new OrgTreeNode(
                -куратор.Id, куратор.FullName, куратор.Position, null,
                Head: null, Curator: null, StaffCount: 0, FromPortal: false, Children: [],
                IsPerson: true, Staff: []);

            узлыЛюдей[куратор.Id] = узел;

            if (куратор.OrgUnitId is int unitId && nodes.TryGetValue(unitId, out var подразделение))
                подразделение.Children.Add(узел);
            else
                roots.Add(узел);
        }

        foreach (var (curatorId, node) in подчинённые)
        {
            if (!узлыЛюдей.TryGetValue(curatorId, out var человек))
            {
                // Куратор указан, но такого пользователя нет — подразделение
                // осталось бы невидимым, поэтому показываем его верхним уровнем.
                roots.Add(node);
                continue;
            }

            // Куратор числится в подразделении, которое сам же курирует, — его
            // узел уже стоит под ним. Повесить это подразделение ещё и под
            // человека значит замкнуть кольцо, на котором обход дерева не
            // кончится. Проверяем не прямое совпадение, а достижимость: кольцо
            // бывает и длиннее — через второго куратора. Такое подразделение и
            // есть верхний уровень, им и остаётся.
            if (Достижим(node, человек))
            {
                roots.Add(node);
                continue;
            }

            человек.Children.Add(node);
        }
    }

    /// <summary>Стоит ли искомый узел где-то ниже данного.</summary>
    private static bool Достижим(OrgTreeNode from, OrgTreeNode target)
    {
        if (ReferenceEquals(from, target)) return true;

        var очередь = new Queue<OrgTreeNode>([from]);

        while (очередь.Count > 0)
            foreach (var ребёнок in очередь.Dequeue().Children)
            {
                if (ReferenceEquals(ребёнок, target)) return true;
                очередь.Enqueue(ребёнок);
            }

        return false;
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
