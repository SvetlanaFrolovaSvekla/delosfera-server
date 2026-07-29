using System.Security.Claims;

namespace delosfera_server.Common.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    public int UserId =>
        int.Parse(_accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new UnauthorizedAccessException("Пользователь не аутентифицирован"));
}