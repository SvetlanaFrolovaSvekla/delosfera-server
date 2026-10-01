using Microsoft.EntityFrameworkCore;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

/// <summary>
/// Подразделение вместе со всеми вложенными (на любую глубину).
///
/// После синхронизации оргструктуры сотрудники числятся в отделах и секторах,
/// а выбирают обычно департамент или управление целиком. Сравнение по точному
/// id в таких местах давало пустой список и отказ «согласующий не из того
/// подразделения», хотя человек работает внутри него.
///
/// Справочник подразделений — сотни строк, поэтому пары (Id, ParentId) читаются
/// одним запросом, а обход идёт в памяти. Защищено от циклов в данных.
/// </summary>
public static class OrgUnitSubtree
{
    public static async Task<HashSet<int>> CollectAsync(
        IQueryable<OrganizationUnit> units, IEnumerable<int> rootIds, CancellationToken ct = default)
    {
        var roots = rootIds.Distinct().ToList();
        var result = new HashSet<int>();
        if (roots.Count == 0) return result;

        var pairs = await units
            .AsNoTracking()
            .Where(u => u.ParentId != null)
            .Select(u => new { u.Id, ParentId = u.ParentId!.Value })
            .ToListAsync(ct);

        var childrenByParent = pairs
            .GroupBy(p => p.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var stack = new Stack<int>(roots);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id)) continue;
            if (childrenByParent.TryGetValue(id, out var children))
                foreach (var child in children) stack.Push(child);
        }

        return result;
    }

    public static Task<HashSet<int>> CollectAsync(
        IQueryable<OrganizationUnit> units, int rootId, CancellationToken ct = default)
        => CollectAsync(units, new[] { rootId }, ct);
}
