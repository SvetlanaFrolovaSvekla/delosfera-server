using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Роли, которыми в шаблоне маршрута задаётся согласующий.
///
/// Шаблон должен переживать смену людей в должностях: если маршрут указывает на
/// «руководителя подразделения автора», он остаётся верным и после того, как
/// руководитель сменился. Поимённо задают только тех, кто действительно
/// незаменим на этом этапе.
/// </summary>
public static class RouteRoles
{
    /// <summary>Руководитель подразделения, в котором работает автор документа.</summary>
    public const string AuthorHead = "author-head";

    /// <summary>Куратор подразделения автора — курирующий зампред по приказу о полномочиях.</summary>
    public const string AuthorCurator = "author-curator";

    /// <summary>Руководитель подразделения, выбранного в самом документе.</summary>
    public const string TargetUnitHead = "target-unit-head";

    /// <summary>Куратор подразделения, выбранного в документе.</summary>
    public const string TargetUnitCurator = "target-unit-curator";

    /// <summary>Председатель Правления — из состава коллегиального органа.</summary>
    public const string BoardChairman = "board-chairman";

    /// <summary>Руководитель указанного подразделения: «unit-head:44».</summary>
    public const string UnitHeadPrefix = "unit-head:";

    /// <summary>Куратор указанного подразделения: «unit-curator:44».</summary>
    public const string UnitCuratorPrefix = "unit-curator:";

    /// <summary>Человеческие названия для экрана настройки.</summary>
    public static string Title(string roleRef) => roleRef switch
    {
        AuthorHead => "Руководитель подразделения автора",
        AuthorCurator => "Куратор подразделения автора",
        TargetUnitHead => "Руководитель подразделения из документа",
        TargetUnitCurator => "Куратор подразделения из документа",
        BoardChairman => "Председатель Правления",
        _ when roleRef.StartsWith(UnitHeadPrefix) => "Руководитель подразделения",
        _ when roleRef.StartsWith(UnitCuratorPrefix) => "Куратор подразделения",
        _ => roleRef,
    };
}

/// <summary>Что известно о документе в момент сборки маршрута.</summary>
public record RouteContext(int DocumentId, int? AuthorUnitId, int? TargetUnitId);

public interface IRouteRoleResolver
{
    /// <summary>
    /// Кто стоит за ролью. Null — роль не разрешилась: в справочнике не заполнен
    /// руководитель, куратор или состав органа.
    /// </summary>
    Task<int?> ResolveAsync(string roleRef, RouteContext context, CancellationToken ct = default);

    /// <summary>Почему роль не разрешилась — текст для человека, а не для журнала.</summary>
    string Explain(string roleRef);
}

/// <summary>
/// Разрешает роли шаблона в конкретных людей при запуске маршрута.
///
/// До сих пор роль копировалась в участника как есть и никогда не превращалась в
/// человека: задача не создавалась, участник числился активным, и маршрут вставал
/// навсегда — молча, потому что формально всё было в порядке.
/// </summary>
public class RouteRoleResolver : IRouteRoleResolver
{
    private readonly DelosferaDbContext _db;

    public RouteRoleResolver(DelosferaDbContext db) => _db = db;

    public async Task<int?> ResolveAsync(
        string roleRef, RouteContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleRef)) return null;

        if (roleRef == RouteRoles.AuthorHead)
            return await HeadOfAsync(context.AuthorUnitId, ct);

        if (roleRef == RouteRoles.AuthorCurator)
            return await CuratorOfAsync(context.AuthorUnitId, ct);

        if (roleRef == RouteRoles.TargetUnitHead)
            return await HeadOfAsync(context.TargetUnitId, ct);

        if (roleRef == RouteRoles.TargetUnitCurator)
            return await CuratorOfAsync(context.TargetUnitId, ct);

        if (roleRef == RouteRoles.BoardChairman)
            return await _db.BodyMembers.AsNoTracking()
                .Where(m => m.Body == Meetings.Models.MeetingBody.Board
                            && m.Role == Meetings.Models.BodyRole.Chairman)
                .Select(m => (int?) m.UserId)
                .FirstOrDefaultAsync(ct);

        if (Unit(roleRef, RouteRoles.UnitHeadPrefix) is { } headUnit)
            return await HeadOfAsync(headUnit, ct);

        if (Unit(roleRef, RouteRoles.UnitCuratorPrefix) is { } curatorUnit)
            return await CuratorOfAsync(curatorUnit, ct);

        return null;
    }

    public string Explain(string roleRef) => roleRef switch
    {
        RouteRoles.AuthorHead =>
            "не заполнен руководитель подразделения автора",
        RouteRoles.AuthorCurator =>
            "не заполнен куратор подразделения автора",
        RouteRoles.TargetUnitHead =>
            "не заполнен руководитель подразделения, выбранного в документе",
        RouteRoles.TargetUnitCurator =>
            "не заполнен куратор подразделения, выбранного в документе",
        RouteRoles.BoardChairman =>
            "в составе Правления не назначен председатель",
        _ when roleRef.StartsWith(RouteRoles.UnitHeadPrefix) =>
            "не заполнен руководитель подразделения",
        _ when roleRef.StartsWith(RouteRoles.UnitCuratorPrefix) =>
            "не заполнен куратор подразделения",
        _ => $"неизвестная роль «{roleRef}»",
    };

    // ── вспомогательное ──────────────────────────────────────────────────────

    private static int? Unit(string roleRef, string prefix) =>
        roleRef.StartsWith(prefix) && int.TryParse(roleRef[prefix.Length..], out var id) ? id : null;

    private async Task<int?> HeadOfAsync(int? unitId, CancellationToken ct) =>
        unitId is null
            ? null
            : await _db.OrganizationUnits.AsNoTracking()
                .Where(u => u.Id == unitId)
                .Select(u => u.HeadUserId)
                .FirstOrDefaultAsync(ct);

    private async Task<int?> CuratorOfAsync(int? unitId, CancellationToken ct) =>
        unitId is null
            ? null
            : await _db.OrganizationUnits.AsNoTracking()
                .Where(u => u.Id == unitId)
                .Select(u => u.CuratorUserId)
                .FirstOrDefaultAsync(ct);
}
