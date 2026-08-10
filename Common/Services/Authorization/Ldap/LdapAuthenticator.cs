using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Options;

namespace delosfera_server.Common.Services.Authorization.Ldap;

public class LdapAuthenticator : ILdapAuthenticator
{
    private const int InvalidCredentialsErrorCode = 49;
    private readonly LdapOptions _options;
    private readonly ILogger<LdapAuthenticator> _logger;

    public LdapAuthenticator(IOptions<LdapOptions> options, ILogger<LdapAuthenticator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Проверяет пароль пользователя простым Bind() к LDAP-серверу, без чтения атрибутов
    
    /*Т.е всё то же самое подключение, что и в LdapDirectoryService, только теперь Bind()
    делается не под сервисным аккаунтом, а под тем логином/паролем, которые передал 
    вызывающий код (то есть данные, которые пользователь ввёл в форму входа на сайте). 
    Если Bind() отработал без исключения - значит AD подтвердил, что пароль верный,
    и метод возвращает true.*/
    public Task<bool> VerifyPasswordAsync(string login, string password)
    {
        return Task.Run(() =>
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

                // login тут - значение из LoginAttribute (sAMAccountName)
                connection.Bind(new NetworkCredential(login, password));
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
}