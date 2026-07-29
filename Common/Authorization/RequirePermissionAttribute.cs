using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Common.Authorization;

/// <summary>
/// Требует, чтобы у пользователя (через его роли) было указанное право.
/// Проверяется claim "permission" в JWT-токене.
/// </summary>
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly PermissionCode _permission;

    public RequirePermissionAttribute(PermissionCode permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        var hasPermission = user.Claims
            .Where(c => c.Type == "permission")
            .Select(c => int.Parse(c.Value))
            .Contains((int)_permission);

        if (!hasPermission)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
        }
    }
}