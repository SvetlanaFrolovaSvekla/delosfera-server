using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Options;
using delosfera_server.Modules.Integrations.Directory;

namespace delosfera_server.Common.Services.Authorization.Ldap;

public class LdapDirectoryService : ILdapDirectoryService
{
    private const int
        UserAccountControlDisabledBit =
            2; // флаг "учётка отключена", тот же флаг, что в Terrasoft (LdapDisabledAccountValue)

    private readonly IDirectorySettingsService _settings;
    private readonly ILogger<LdapDirectoryService> _logger;

    public LdapDirectoryService(IDirectorySettingsService settings, ILogger<LdapDirectoryService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    // В System.DirectoryServices.Protocols нет современных нативных async/await методов вроде SendRequestAsync
    // Используется Task.Run
    public async Task<List<LdapDirectoryUser>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var _options = await _settings.GetEffectiveAsync(ct)
            ?? throw new InvalidOperationException(
                "Связь со службой каталогов не настроена или выключена");

        return await Task.Run(() =>
        {
            using var connection = new LdapConnection(new LdapDirectoryIdentifier(_options.Server, _options.Port))
            {
                AuthType = AuthType.Basic // Всегда Basic, всегда простой bind
            };
            connection.SessionOptions.ProtocolVersion = 3;

            /* Если в конфиге UseSsl: true (по умолчанию) - включаем шифрование и подставляем
            свою функцию проверки сертификата сервера, потому что банковский внутренний CA
            стандартной проверкой .NET не распознаётся как доверенный
            Поэтому используется VerifyServerCertificate (собственная проверка вместо стандартной) (как и в терасофт).
             */
            if (_options.UseSsl)
            {
                connection.SessionOptions.SecureSocketLayer = true;
                connection.SessionOptions.VerifyServerCertificate = (conn, cert) => LdapCertificateValidator.VerifyCorporateCertificate(cert);
            }

            // Синк ходит под техническим аккаунтом, не под учёткой конкретного юзера
            connection.Bind(new NetworkCredential(_options.ServiceAccountLogin, _options.ServiceAccountPassword));


            // Перечисляются необходимые для получения атрибуты (как в GetUserAttributes в терасофт)
            // логин, email, ФИО, уникальный ID (objectGUID), статус аккаунта userAccountControl
            var result = new List<LdapDirectoryUser>();
            var attributes = new[]
            {
                _options.LoginAttribute, _options.EmailAttribute, _options.FullNameAttribute,
                _options.PositionAttribute, _options.OrgUnitAttribute,
                "objectGUID", "userAccountControl"
            };

            // искать начиная от UsersBaseDn по фильтру UsersFilter (только реальные люди),
            // просматривая всё поддерево, вернуть только перечисленные attributes".
            var request = new SearchRequest(
                _options.UsersBaseDn, _options.UsersFilter, SearchScope.Subtree, attributes);

            // Постраничный обход - ровно та же схема, что и в Terrasoft (PageResultRequestControl + cookie),
            // Стандартная часть LDAP-протокола
            var paging = new PageResultRequestControl(_options.PageSize);
            request.Controls.Add(paging);

            /* Цикл do...while продолжается, пока сервер не пришлёт пустой cookie -
            это сигнал "страниц больше нет, это была последняя" */
            do
            {
                ct.ThrowIfCancellationRequested();

                SearchResponse response;
                try
                {
                    response = (SearchResponse)connection.SendRequest(request);
                }
                catch (LdapException ex)
                {
                    _logger.LogError(ex, "LDAP search request failed");
                    throw;
                }

                foreach (SearchResultEntry entry in response.Entries)
                {
                    var user = MapEntry(entry, _options);
                    if (user is not null) result.Add(user);
                }

                var pageControl = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault();

                paging.Cookie = pageControl?.Cookie ?? [];
            } while (paging.Cookie.Length != 0);

            _logger.LogInformation("LDAP: получено {Count} пользователей из директории", result.Count);
            return result;
        }, ct);
    }


    // Метод для превращения AD-записи пользователя в объект User
    private LdapDirectoryUser? MapEntry(SearchResultEntry entry, Common.Options.LdapOptions _options)
    {
        var login = entry.Attributes[_options.LoginAttribute]?[0]?.ToString();
        var email = entry.Attributes[_options.EmailAttribute]?[0]?.ToString();
        var fullName = entry.Attributes[_options.FullNameAttribute]?[0]?.ToString();
        var guidBytes = entry.Attributes["objectGUID"]?[0] as byte[];

        if (login is null || email is null || guidBytes is null)
        {
            _logger.LogWarning("LDAP: запись {Dn} пропущена — не хватает обязательных атрибутов",
                entry.DistinguishedName);
            return null;
        }

        // userAccountControl - комбинацию битов превращаем в строку, далее в int. 
        // Если uac - 2, то активный; если пришел null, то считаем юзера активным по умолчанию
        var uac = entry.Attributes["userAccountControl"]?[0]?.ToString();
        var isActive = uac is null || (int.Parse(uac) & UserAccountControlDisabledBit) == 0;

        return new LdapDirectoryUser(
            new Guid(guidBytes), login, email, fullName ?? login, isActive,
            Position: Text(entry, _options.PositionAttribute),
            Department: Text(entry, _options.OrgUnitAttribute));
    }

    /// <summary>Значение атрибута или null, если он не заполнен в каталоге.</summary>
    private static string? Text(SearchResultEntry entry, string attribute)
    {
        var value = entry.Attributes[attribute]?[0]?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}