namespace delosfera_server.Common.Services;

public interface ICurrentUserService
{
    int UserId { get; }

    /// <summary>Есть ли у текущего пользователя указанное право (по claim "permission" в JWT)</summary>
    bool HasPermission(Modules.Users.Models.PermissionCode permission);
}