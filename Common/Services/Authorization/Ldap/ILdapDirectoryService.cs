namespace delosfera_server.Common.Services.Authorization.Ldap;

/// <summary>
/// Сотрудник, как его видит служба каталогов. Должность и подразделение приходят
/// оттуда же: в домене они уже заполнены кадровой службой, и держать их вручную
/// во второй раз — значит заводить второй источник правды.
/// </summary>
public record LdapDirectoryUser(
    Guid ObjectId, string Login, string Email, string FullName, bool IsActive,
    string? Position = null, string? Department = null);

public interface ILdapDirectoryService
{
    // Получение всех пользователей с AD
    Task<List<LdapDirectoryUser>> GetAllUsersAsync(CancellationToken ct = default);
}