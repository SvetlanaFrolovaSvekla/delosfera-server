namespace delosfera_server.Common.Options;

public class LdapOptions
{
    // Подключение
    public required string Server { get; set; }       // адрес AD-сервера: "dc01.keremetbank.kg"
    
    /* Порт LDAP. 636 - это стандартный порт для LDAPS (LDAP через SSL),
     обычный незашифрованный LDAP обычно на 389 */
    public int Port { get; set; } = 636; 
    
    /* Включать ли шифрование соединения */
    public bool UseSsl { get; set; } = true;

    // Технический аккаунт для поиска (bind service-account, используется в LdapDirectoryService)
    // Т.е отдельный сервисный аккаунт с правами "только читать" каталог AD.
    
    // Указывается в secrets.json ( <UserSecretsId> в delosfera-server.csproj). Там указываются данные:
    /*{
        "Ldap:ServiceAccountLogin": "svc-delosfera-ldap",
        "Ldap:ServiceAccountPassword": "реальный-пароль-ldap"
    }*/
    public required string ServiceAccountLogin { get; set; }
    public required string ServiceAccountPassword { get; set; }

    // Где искать пользователей
    
    // UsersBaseDn - это "корневая папка" в дереве AD, откуда начинать поиск
    // это DN той ветки каталога, где лежат учётки сотрудников банка
    public required string UsersBaseDn { get; set; }    // "OU=Users,DC=keremetbank,DC=kg"
    
    // LDAP-фильтр, который отсеивает "это объект-пользователь, а не компьютер/группа/что-то ещё"
    public string UsersFilter { get; set; } = "(&(objectClass=user)(objectCategory=person))";
    // сколько записей забирать за один "заход" к серверу
    public int PageSize { get; set; } = 500;

    // Атрибуты AD
    public string LoginAttribute { get; set; } = "sAMAccountName"; // sfrolova
    public string EmailAttribute { get; set; } = "mail"; // keremetbank.kg
    public string FullNameAttribute { get; set; } = "displayName"; // полное отображаемое имя (ФИО)

    // Должность сотрудника — в Active Directory это title.
    public string PositionAttribute { get; set; } = "title";

    // Отдел — в Active Directory это department. Атрибут ou описывает лишь ветку
    // дерева каталога и оргструктуре банка не соответствует.
    public string OrgUnitAttribute { get; set; } = "department";

    // Фоновый синк
    public int SyncIntervalMinutes { get; set; } = 60; // как часто гонять фоновую синхронизацию, 60 мин
    public int? DefaultRoleId { get; set; } // Id роли "Рядовой пользователь" для новых LDAP-юзеров (в конфиге это значение - 2)
    
    // Флаг, чтобы не засорять логи и не гонять фоновые попытки конекта с LDAP (dev)
    public bool Enabled { get; set; } = true;
}