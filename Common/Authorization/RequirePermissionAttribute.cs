using Microsoft.AspNetCore.Mvc.Filters;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Common.Authorization;

/// <summary>
/// Требует, чтобы у пользователя (через его роли) было указанное право.
/// Проверяется claim "permission" в JWT-токене.
/// </summary>
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly PermissionCode[] _permissions;

    /// <summary>
    /// Требует любое из перечисленных прав (логическое ИЛИ). С одним аргументом ведёт
    /// себя как прежде; несколько — когда один эндпоинт законно доступен нескольким
    /// ролям (например, список сотрудников нужен и администратору пользователей, и
    /// администратору справочников ВНД для назначения ответственных).
    /// </summary>
    public RequirePermissionAttribute(params PermissionCode[] permissions)
    {
        _permissions = permissions;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        var granted = user.Claims
            .Where(c => c.Type == "permission")
            .Select(c => int.TryParse(c.Value, out var code) ? code : (int?)null)
            .ToHashSet();

        var hasPermission = _permissions.Any(p => granted.Contains((int)p));

        if (!hasPermission)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
        }
    }
}