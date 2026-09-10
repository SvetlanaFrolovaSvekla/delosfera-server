using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Кому какие записки видны.
///
/// Правило одно на все способы добраться до записки. Оно стояло в реестре, но
/// поиск шёл мимо него: по одному слову из текста любой сотрудник читал любую
/// записку — о переводе, об окладе, о взыскании. Ограничение, обойти которое
/// можно строкой поиска, ничего не ограничивает.
/// </summary>
public static class SzVisibility
{
    /// <summary>
    /// Сузить выборку до записок, доступных этому пользователю.
    ///
    /// Круги доступа: свои; подразделения, которыми руководит; пришедшие на
    /// согласование; где назначен адресатом или подписантом; по которым выдано
    /// поручение. Право «видеть все записки» снимает сужение целиком.
    /// </summary>
    public static async Task<IQueryable<SzDocument>> ApplyAsync(
        IQueryable<SzDocument> query,
        DelosferaDbContext db,
        int currentUserId,
        bool canSeeAll,
        bool canSeeOthersDrafts)
    {
        // Чужой черновик видит только администратор системы: черновик — ещё не
        // документ, и автор вправе передумать, ни перед кем не объясняясь.
        if (!canSeeOthersDrafts)
            query = query.Where(x => x.Document!.StatusCode != SzStatus.Draft
                                     || x.Document!.AuthorId == currentUserId);

        if (canSeeAll) return query;

        var units = await VisibleUnitIdsAsync(db, currentUserId);

        return query.Where(x =>
            x.Document!.AuthorId == currentUserId
            || (x.AuthorUnitId != null && units.Contains(x.AuthorUnitId.Value))
            || x.Approvers.Any(a => a.UserId == currentUserId)
            || db.RouteParticipants.Any(p => p.UserId == currentUserId
                                             && p.RouteStep!.RouteInstance!.DocumentId == x.DocumentId)
            || x.AddresseeUserId == currentUserId
            || x.SignerUserId == currentUserId
            || x.Assignments.Any(a => a.AssigneeUserId == currentUserId));
    }

    /// <summary>
    /// Вправе ли пользователь работать с конкретной запиской вне маршрута
    /// согласования: бумажный оригинал, архив, передача в закупку, печатная форма.
    /// Круг тот же, что и видимость в реестре, но уже: автор и руководитель
    /// подразделения-автора. <paramref name="canManageAll"/> — делопроизводственное
    /// право (RegisterSz/ViewAllSz/ViewAllProcurements), снимающее привязку целиком.
    ///
    /// До этой проверки любой аутентифицированный сотрудник мог выдать чужой
    /// оригинал, подшить чужую записку в дело или запустить по ней закупку.
    /// </summary>
    public static async Task<bool> CanAccessAsync(
        DelosferaDbContext db, SzDocument sz, int currentUserId, bool canManageAll)
    {
        if (canManageAll) return true;
        if (sz.Document!.AuthorId == currentUserId) return true;
        if (sz.AuthorUnitId is int unit)
            return (await VisibleUnitIdsAsync(db, currentUserId)).Contains(unit);
        return false;
    }

    /// <summary>
    /// Подразделения, записки которых видит руководитель: его собственное и все
    /// вложенные. Управление отвечает за свои отделы, значит и видеть должно их.
    ///
    /// Пусто, если человек ничем не руководит.
    /// </summary>
    public static async Task<List<int>> VisibleUnitIdsAsync(DelosferaDbContext db, int currentUserId)
    {
        var headed = await db.OrganizationUnits
            .Where(u => u.HeadUserId == currentUserId)
            .Select(u => u.Id)
            .ToListAsync();

        if (headed.Count == 0) return headed;

        // Дерево читаем целиком один раз: подразделений пара сотен, а спуск по
        // родителям запросом на каждый уровень — это запрос на каждый уровень.
        var all = await db.OrganizationUnits
            .Select(u => new {u.Id, u.ParentId})
            .ToListAsync();

        var byParent = all
            .Where(u => u.ParentId != null)
            .GroupBy(u => u.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToList());

        var result = new HashSet<int>(headed);
        var queue = new Queue<int>(headed);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byParent.TryGetValue(current, out var children)) continue;

            foreach (var child in children)
                if (result.Add(child)) queue.Enqueue(child);
        }

        return result.ToList();
    }
}
