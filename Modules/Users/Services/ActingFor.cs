using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Users.Services;

/// <summary>
/// За кого пользователь сейчас действует по замещению (GEN-14).
///
/// Одно правило на все контуры: реестр задач показывал замещающему чужие задачи,
/// а карточка записки и действия по ней проверяли только его собственный id, и
/// замещающий видел работу, которую не мог ни открыть, ни выполнить. Здесь — тот же
/// запрос, что и у <see cref="SubstitutionService.GetActingForUserIdsAsync"/>, но без
/// зависимости от сервиса, чтобы проверку могли звать и статические правила
/// видимости, и сервисы, которые собираются вручную (в тестах в том числе).
/// </summary>
public static class ActingFor
{
    /// <summary>Отсутствующие сотрудники, которых <paramref name="substituteUserId"/> замещает в день <paramref name="today"/>.</summary>
    public static Task<List<int>> AbsentUserIdsAsync(DelosferaDbContext db, int substituteUserId, DateOnly today) =>
        db.Substitutions.AsNoTracking()
            .Where(s => s.SubstituteUserId == substituteUserId && !s.IsCancelled
                        && s.StartsOn <= today && s.EndsOn >= today)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();

    /// <summary>
    /// Кто сейчас замещает <paramref name="absentUserId"/> — им, как и самому отсутствующему,
    /// адресуются уведомления о его задачах: иначе о задаче узнаёт только тот, кто в отпуске.
    /// </summary>
    public static Task<List<int>> SubstituteIdsAsync(DelosferaDbContext db, int absentUserId, DateOnly today) =>
        db.Substitutions.AsNoTracking()
            .Where(s => s.UserId == absentUserId && !s.IsCancelled
                        && s.StartsOn <= today && s.EndsOn >= today)
            .Select(s => s.SubstituteUserId)
            .Distinct()
            .ToListAsync();

    /// <summary>Сам пользователь и все, за кого он сейчас действует.</summary>
    public static async Task<List<int>> SelfAndAbsentAsync(DelosferaDbContext db, int userId, DateOnly today)
    {
        var ids = await AbsentUserIdsAsync(db, userId, today);
        if (!ids.Contains(userId)) ids.Add(userId);
        return ids;
    }

    /// <summary>
    /// Вправе ли <paramref name="actorUserId"/> действовать за <paramref name="principalUserId"/>:
    /// это он сам или его действующий замещающий. Пустой principal — нет.
    /// </summary>
    public static async Task<bool> ActsForAsync(
        DelosferaDbContext db, int actorUserId, int? principalUserId, DateOnly today)
    {
        if (principalUserId is not int principal) return false;
        if (principal == actorUserId) return true;
        return (await AbsentUserIdsAsync(db, actorUserId, today)).Contains(principal);
    }
}
