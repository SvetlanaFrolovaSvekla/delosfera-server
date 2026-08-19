namespace delosfera_server.Modules.Integrations.Directory;

/// <summary>
/// Настройки службы каталогов (INT-01).
///
/// Имена атрибутов вынесены в конфигурацию, а не зашиты в код: у Active Directory
/// логин лежит в sAMAccountName, у OpenLDAP — в uid, а подразделение банк может
/// вести как в ou, так и в department. Менять это должен админ, а не сборка.
/// </summary>
public class LdapOptions
{
    public const string Section = "Ldap";

    /// <summary>Интеграция включена. Выключена — домениый вход и синхронизация отвечают отказом.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;

    /// <summary>LDAPS или StartTLS. В контуре банка обязателен: по каналу идут пароли.</summary>
    public bool UseSsl { get; set; }

    /// <summary>Корень поиска, например dc=keremetbank,dc=kg.</summary>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>Служебная учётная запись для чтения каталога.</summary>
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;

    /// <summary>Фильтр выборки сотрудников.</summary>
    public string UserFilter { get; set; } = "(objectClass=inetOrgPerson)";

    /// <summary>Атрибут логина: sAMAccountName в AD, uid в OpenLDAP.</summary>
    public string LoginAttribute { get; set; } = "uid";

    public string EmailAttribute { get; set; } = "mail";
    public string FullNameAttribute { get; set; } = "cn";
    public string PositionAttribute { get; set; } = "title";
    // В Active Directory отдел хранится в department; ou описывает лишь ветку
    // дерева каталога и оргструктуре банка не соответствует.
    public string OrgUnitAttribute { get; set; } = "department";

    /// <summary>
    /// Атрибут признака блокировки в AD (userAccountControl). Пусто — признак не читается,
    /// и уволенные определяются только по исчезновению из выборки.
    /// </summary>
    public string? DisabledAttribute { get; set; }

    /// <summary>
    /// Заводить ли в системе сотрудников, которых нет в базе. Выключено — синхронизация
    /// только обновляет уже заведённых: полезно на первом запуске, чтобы не втянуть
    /// весь каталог целиком.
    /// </summary>
    public bool CreateMissingUsers { get; set; } = true;

    /// <summary>
    /// Создавать ли подразделения, которых нет в справочнике. По умолчанию нет:
    /// оргструктура банка ведётся вручную с историчностью, и плодить её из строк
    /// каталога — верный способ получить дубли «УИТ» и «Управление ИТ».
    /// </summary>
    public bool CreateMissingOrgUnits { get; set; }

    /// <summary>Роль по умолчанию для заведённых из каталога сотрудников.</summary>
    public string DefaultRoleTitleRu { get; set; } = "Рядовой пользователь";
}
