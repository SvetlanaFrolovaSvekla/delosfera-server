using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Options;
using delosfera_server.Modules.Integrations.Directory;

namespace delosfera_server.Common.Services.Authorization.Ldap;

public class LdapAuthenticator : ILdapAuthenticator
{
    private const int InvalidCredentialsErrorCode = 49;
    private readonly IDirectorySettingsService _settings;
    private readonly ILogger<LdapAuthenticator> _logger;

    public LdapAuthenticator(IDirectorySettingsService settings, ILogger<LdapAuthenticator> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    // Проверяет пароль пользователя простым Bind() к LDAP-серверу, без чтения атрибутов
    
    /*Т.е всё то же самое подключение, что и в LdapDirectoryService, только теперь Bind()
    делается не под сервисным аккаунтом, а под тем логином/паролем, которые передал 
    вызывающий код (то есть данные, которые пользователь ввёл в форму входа на сайте). 
    Если Bind() отработал без исключения - значит AD подтвердил, что пароль верный,
    и метод возвращает true.*/
    public async Task<bool> VerifyPasswordAsync(string login, string password)
    {
        // Адрес каталога задаётся администратором в интерфейсе и может измениться
        // между попытками входа — читаем его при каждой проверке.
        var _options = await _settings.GetEffectiveAsync();
        if (_options is null)
        {
            _logger.LogWarning("Доменный вход: связь со службой каталогов не настроена");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var connection = new LdapConnection(
                    new LdapDirectoryIdentifier(_options.Server, _options.Port))
                {
                    AuthType = AuthType.Basic
                };
                connection.SessionOptions.ProtocolVersion = 3;

                if (_options.UseSsl)
                {
                    connection.SessionOptions.SecureSocketLayer = true;
                    connection.SessionOptions.VerifyServerCertificate = (conn, cert) => LdapCertificateValidator.VerifyCorporateCertificate(cert);
                }

                // Домен принимает простой bind только по имени в форме имя@домен:
                // на голый sAMAccountName он отвечает отказом с кодом 52e, и вход
                // выглядел бы как неверный пароль.
                connection.Bind(new NetworkCredential(ToUserPrincipalName(login, _options), password));
                return true;
            }
            catch (LdapException ex) when (ex.ErrorCode == InvalidCredentialsErrorCode)
            {
                return false; // неверный пароль
            }
            catch (LdapException ex)
            {
                _logger.LogError(ex, "LDAP bind error for {Login}", login);
                throw; // сервер недоступен
            }
        });
    }

    /// <summary>
    /// Привести логин к виду имя@домен, который принимает служба каталогов.
    ///
    /// Домен берётся из учётной записи для чтения каталога, а если она задана без
    /// домена — из ветки поиска: DC=kgbank,DC=local означает домен kgbank.local.
    /// Если домен определить неоткуда, логин уходит как есть — хуже, чем было, не станет.
    /// </summary>
    private static string ToUserPrincipalName(string login, Common.Options.LdapOptions options)
    {
        if (login.Contains('@') || login.Contains('\\')) return login;

        var domain = DomainFromLogin(options.ServiceAccountLogin) ?? DomainFromBaseDn(options.UsersBaseDn);
        return domain is null ? login : $"{login}@{domain}";
    }

    private static string? DomainFromLogin(string? serviceAccountLogin)
    {
        var at = serviceAccountLogin?.IndexOf('@') ?? -1;
        return at > 0 ? serviceAccountLogin![(at + 1)..] : null;
    }

    private static string? DomainFromBaseDn(string? baseDn)
    {
        if (string.IsNullOrWhiteSpace(baseDn)) return null;

        var parts = baseDn
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[3..])
            .ToArray();

        return parts.Length > 0 ? string.Join('.', parts) : null;
    }
}