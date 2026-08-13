using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;

namespace delosfera_server.Modules.Integrations.Directory;

/// <summary>Сотрудник, как его видит служба каталогов.</summary>
public record DirectoryEntry(
    string Login,
    string? Email,
    string? FullName,
    string? Position,
    string? OrgUnit,
    bool IsDisabled);

public interface ILdapDirectory
{
    bool Enabled { get; }

    /// <summary>Выгрузить сотрудников из каталога.</summary>
    Task<List<DirectoryEntry>> ListUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// Проверить логин и пароль привязкой к каталогу. Возвращает запись сотрудника
    /// либо null, если пара не подошла.
    /// </summary>
    Task<DirectoryEntry?> AuthenticateAsync(string login, string password, CancellationToken ct = default);
}

/// <summary>
/// Служба каталогов AD/LDAP (INT-01).
///
/// Пароль не проверяется сравнением хешей: система привязывается к каталогу от имени
/// самого сотрудника. Так пароль остаётся в домене — банк не хранит его копию и не
/// обходит доменные политики блокировки и смены.
/// </summary>
public class LdapDirectory : ILdapDirectory
{
    private readonly LdapOptions _configured;
    private readonly IDirectorySettingsService _settings;

    /// <summary>Настройки текущей операции: общие из базы, пока не прочитаны — из конфигурации.</summary>
    private LdapOptions? _effective;
    private LdapOptions _options => _effective ?? _configured;
    private readonly ILogger<LdapDirectory> _logger;

    public LdapDirectory(
        IOptions<LdapOptions> options,
        IDirectorySettingsService settings,
        ILogger<LdapDirectory> logger)
    {
        // Настройки задаются администратором в разделе системных настроек; значения
        // из конфигурации остаются запасными, чтобы стенд без базы настроек работал.
        _configured = options.Value;
        _settings = settings;
        _logger = logger;
    }

    public bool Enabled => _configured.Enabled || _settings.GetAsync().GetAwaiter().GetResult().Enabled;

    /// <summary>
    /// Действующие параметры связи. Общие настройки системы важнее конфигурации:
    /// иначе доменный вход ходил бы на один каталог, а синхронизация — на другой.
    /// </summary>
    private async Task<LdapOptions> EffectiveAsync(CancellationToken ct)
    {
        var shared = await _settings.GetEffectiveAsync(ct);
        if (shared is null) return _options;

        return new LdapOptions
        {
            Enabled = true,
            Host = shared.Server,
            Port = shared.Port,
            UseSsl = shared.UseSsl,
            BaseDn = shared.UsersBaseDn,
            BindDn = shared.ServiceAccountLogin,
            BindPassword = shared.ServiceAccountPassword,
            UserFilter = shared.UsersFilter,
            LoginAttribute = shared.LoginAttribute,
            EmailAttribute = shared.EmailAttribute,
            FullNameAttribute = shared.FullNameAttribute,
            PositionAttribute = _configured.PositionAttribute,
            OrgUnitAttribute = _configured.OrgUnitAttribute,
            DisabledAttribute = _configured.DisabledAttribute,
            CreateMissingUsers = _configured.CreateMissingUsers,
            CreateMissingOrgUnits = _configured.CreateMissingOrgUnits,
            DefaultRoleTitleRu = _configured.DefaultRoleTitleRu,
        };
    }

    public async Task<List<DirectoryEntry>> ListUsersAsync(CancellationToken ct = default)
    {
        _effective = await EffectiveAsync(ct);
        RequireEnabled();

        using var connection = await ConnectAsync(_options.BindDn, _options.BindPassword, ct);

        var entries = await SearchRawAsync(connection, _options.UserFilter, ct);
        return entries.Select(Map).ToList();
    }

    public async Task<DirectoryEntry?> AuthenticateAsync(
        string login, string password, CancellationToken ct = default)
    {
        _effective = await EffectiveAsync(ct);
        RequireEnabled();

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            return null;

        // Человек вводит логин так, как привык входить в рабочую станцию: и коротким
        // именем, и в виде имя@домен, и как ДОМЕН\имя. В каталоге же ищем по короткому
        // имени, поэтому домен отбрасываем — иначе привычная запись означала бы отказ.
        login = ShortLogin(login);

        // Сначала служебной учёткой находим DN сотрудника: привязаться можно только
        // по полному DN, а пользователь вводит короткий логин.
        DirectoryEntry found;
        string dn;

        using (var service = await ConnectAsync(_options.BindDn, _options.BindPassword, ct))
        {
            var filter = $"(&{_options.UserFilter}({_options.LoginAttribute}={Escape(login)}))";
            var entries = await SearchRawAsync(service, filter, ct);

            if (entries.Count == 0)
            {
                _logger.LogInformation("LDAP: учётная запись {Login} в каталоге не найдена", login);
                return null;
            }

            dn = entries[0].Dn;
            found = Map(entries[0]);
        }

        try
        {
            using var user = await ConnectAsync(dn, password, ct);
        }
        catch (LdapException ex)
        {
            _logger.LogInformation("LDAP: неверный пароль для {Login} ({Message})", login, ex.Message);
            return null;
        }

        return found;
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private void RequireEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Интеграция со службой каталогов выключена");
    }

    private async Task<LdapConnection> ConnectAsync(string dn, string password, CancellationToken ct)
    {
        var connection = new LdapConnection {SecureSocketLayer = _options.UseSsl};

        try
        {
            await connection.ConnectAsync(_options.Host, _options.Port, ct);
            await connection.BindAsync(dn, password, ct);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task<List<LdapEntry>> SearchRawAsync(
        LdapConnection connection, string filter, CancellationToken ct)
    {
        var attributes = new[]
        {
            _options.LoginAttribute, _options.EmailAttribute, _options.FullNameAttribute,
            _options.PositionAttribute, _options.OrgUnitAttribute,
            _options.DisabledAttribute ?? "objectClass",
        };

        var results = await connection.SearchAsync(
            _options.BaseDn, LdapConnection.ScopeSub, filter, attributes, typesOnly: false, ct);

        var entries = new List<LdapEntry>();

        while (await results.HasMoreAsync(ct))
        {
            try
            {
                entries.Add(await results.NextAsync(ct));
            }
            catch (LdapReferralException)
            {
                // Отсылки к другим контроллерам домена в выборке сотрудников не нужны:
                // банк синхронизируется с одним каталогом, заданным в настройках.
            }
        }

        return entries;
    }

    private DirectoryEntry Map(LdapEntry entry) => new(
        Login: Value(entry, _options.LoginAttribute) ?? entry.Dn,
        Email: Value(entry, _options.EmailAttribute),
        FullName: Value(entry, _options.FullNameAttribute),
        Position: Value(entry, _options.PositionAttribute),
        OrgUnit: Value(entry, _options.OrgUnitAttribute),
        IsDisabled: IsDisabled(entry));

    private static string? Value(LdapEntry entry, string attribute)
    {
        var value = entry.GetStringValueOrDefault(attribute, null!);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// В Active Directory отключённая учётная запись помечается вторым битом
    /// userAccountControl (0x2). Отдельного булева атрибута там нет.
    /// </summary>
    private bool IsDisabled(LdapEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_options.DisabledAttribute)) return false;

        var raw = Value(entry, _options.DisabledAttribute);
        return int.TryParse(raw, out var flags) && (flags & 0x2) != 0;
    }

    /// <summary>Экранирование спецсимволов фильтра (RFC 4515) — логин приходит от пользователя.</summary>
    /// <summary>
    /// Короткое имя учётной записи из того, что ввёл пользователь:
    /// «имя@домен» и «ДОМЕН\\имя» приводятся к «имя».
    /// </summary>
    private static string ShortLogin(string login)
    {
        login = login.Trim();

        var slash = login.LastIndexOf('\\');
        if (slash >= 0) login = login[(slash + 1)..];

        var at = login.IndexOf('@');
        if (at > 0) login = login[..at];

        return login;
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\5c")
        .Replace("*", "\\2a")
        .Replace("(", "\\28")
        .Replace(")", "\\29")
        .Replace("\0", "\\00");
}
