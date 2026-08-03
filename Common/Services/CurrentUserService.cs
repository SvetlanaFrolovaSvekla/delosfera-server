using System.Security.Claims;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Common.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    public int UserId =>
        int.Parse(_accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new UnauthorizedAccessException("Пользователь не аутентифицирован!"));

    public bool HasPermission(PermissionCode permission)
    {
        var user = _accessor.HttpContext?.User;
        if (user is null) return false;

        return user.Claims
            .Where(c => c.Type == "permission")
            .Select(c => int.TryParse(c.Value, out var code) ? code : (int?)null)
            .Any(code => code == (int)permission);
    }
}