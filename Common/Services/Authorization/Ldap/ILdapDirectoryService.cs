namespace delosfera_server.Common.Services.Authorization.Ldap;

public record LdapDirectoryUser(Guid ObjectId, string Login, string Email, string FullName, bool IsActive);

public interface ILdapDirectoryService
{
    // Получение всех пользователей с AD
    Task<List<LdapDirectoryUser>> GetAllUsersAsync(CancellationToken ct = default);
}