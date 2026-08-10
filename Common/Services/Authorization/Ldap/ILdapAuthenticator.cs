namespace delosfera_server.Common.Services.Authorization.Ldap;

public interface ILdapAuthenticator
{
    /// <summary>Проверяет пароль пользователя простым Bind() к LDAP-серверу, без чтения атрибутов</summary>
    Task<bool> VerifyPasswordAsync(string login, string password);
}