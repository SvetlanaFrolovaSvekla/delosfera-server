using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Секретность ВНД (см. VndDocument.IsSecret). Один источник правды для всех проверок: фильтр
/// контроллеров, VndService, проверка доступа к файлам и проверка подлинности.
/// Секретный документ открыт: обладателям права <see cref="PermissionCode.ManageVndSecrecy"/>
/// (администратор и главный редактор), выбранным пользователям и участникам выбранных групп.
/// </summary>
public static class VndSecrecy
{
    public const string DeniedMessage = "Документ имеет ограниченный доступ";

    public static bool HasFullAccess(ICurrentUserService currentUser) =>
        currentUser.HasPermission(PermissionCode.ManageVndSecrecy);

    /// <summary>Из переданных id — те секретные документы, которые текущий пользователь открыть НЕ
    /// может (глобальный фильтр мягкого удаления игнорируется: удалённый секретный документ
    /// остаётся секретным). Для обладателя права пусто.</summary>
    public static async Task<HashSet<int>> GetDeniedIdsAsync(
        DelosferaDbContext db, ICurrentUserService currentUser, IEnumerable<int> vndIds,
        CancellationToken ct = default)
    {
        var ids = vndIds.Distinct().ToList();
        if (ids.Count == 0 || HasFullAccess(currentUser)) return [];

        var userId = currentUser.UserId;
        var secret = await db.VndDocuments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.IsSecret)
            .Select(x => new
            {
                x.Id,
                Allowed = x.SecretAccessUsers.Any(u => u.Id == userId)
                          || x.SecretAccessGroups.Any(g => g.Users.Any(u => u.Id == userId)),
            })
            .ToListAsync(ct);

        return secret.Where(x => !x.Allowed).Select(x => x.Id).ToHashSet();
    }

    public static async Task<bool> CanViewAsync(
        DelosferaDbContext db, ICurrentUserService currentUser, int vndId, CancellationToken ct = default) =>
        (await GetDeniedIdsAsync(db, currentUser, [vndId], ct)).Count == 0;
}

/// <summary>Вешается на контроллер ВНД: если в маршруте есть id документа (<c>vndId</c>, а при
/// <c>idIsVndId</c> — ещё и <c>id</c>) и документ секретный, а у пользователя нет доступа —
/// запрос отклоняется с 403 до выполнения действия.</summary>
public class VndSecrecyGuardAttribute : TypeFilterAttribute
{
    public VndSecrecyGuardAttribute(bool idIsVndId = false) : base(typeof(VndSecrecyGuardFilter))
    {
        Arguments = [idIsVndId];
    }
}

public class VndSecrecyGuardFilter : IAsyncActionFilter
{
    private readonly bool _idIsVndId;
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VndSecrecyGuardFilter(bool idIsVndId, DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _idIsVndId = idIsVndId;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var values = context.RouteData.Values;
        object? raw = null;
        if (!values.TryGetValue("vndId", out raw) && _idIsVndId)
            values.TryGetValue("id", out raw);

        if (raw is not null && int.TryParse(Convert.ToString(raw), out var vndId))
        {
            bool allowed;
            try
            {
                allowed = await VndSecrecy.CanViewAsync(_db, _currentUser, vndId, context.HttpContext.RequestAborted);
            }
            catch (UnauthorizedAccessException)
            {
                // Не аутентифицирован — это дело [Authorize], не секретности.
                await next();
                return;
            }

            if (!allowed)
            {
                context.Result = new ObjectResult(new { message = VndSecrecy.DeniedMessage, code = "vnd_secret" })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }
        }

        await next();
    }
}
