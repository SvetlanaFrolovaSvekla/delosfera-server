using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Options;
using delosfera_server.Common.Security;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Integrations.Directory;

/// <summary>Настройки каталога для интерфейса. Пароль наружу не отдаётся.</summary>
public class DirectorySettingsDto
{
    public bool Enabled { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string ServiceAccountLogin { get; set; } = string.Empty;

    /// <summary>Пароль задан. Само значение не показывается никому и никогда.</summary>
    public bool HasPassword { get; set; }

    public string UsersBaseDn { get; set; } = string.Empty;
    public string UsersFilter { get; set; } = string.Empty;
    public int PageSize { get; set; }
    public string LoginAttribute { get; set; } = string.Empty;
    public string EmailAttribute { get; set; } = string.Empty;
    public string FullNameAttribute { get; set; } = string.Empty;
    public string PositionAttribute { get; set; } = string.Empty;
    public string OrgUnitAttribute { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; }
    public int? DefaultRoleId { get; set; }

    public DateTime? LastSyncAt { get; set; }
    public int LastSyncCreated { get; set; }
    public int LastSyncUpdated { get; set; }
    public int LastSyncDeactivated { get; set; }
    public string? LastSyncError { get; set; }
}

/// <summary>Изменение настроек. Пустой пароль означает «оставить прежний».</summary>
public class DirectorySettingsRequest
{
    public bool Enabled { get; set; }
    public required string Server { get; set; }
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public required string ServiceAccountLogin { get; set; }

    /// <summary>Новый пароль. Пусто — прежний сохраняется.</summary>
    public string? ServiceAccountPassword { get; set; }

    public required string UsersBaseDn { get; set; }
    public string? UsersFilter { get; set; }
    public int PageSize { get; set; } = 500;
    public string? LoginAttribute { get; set; }
    public string? EmailAttribute { get; set; }
    public string? FullNameAttribute { get; set; }
    public string? PositionAttribute { get; set; }
    public string? OrgUnitAttribute { get; set; }
    public int SyncIntervalMinutes { get; set; } = 60;
    public int? DefaultRoleId { get; set; }
}

public interface IDirectorySettingsService
{
    Task<DirectorySettingsDto> GetAsync(CancellationToken ct = default);
    Task<DirectorySettingsDto> UpdateAsync(DirectorySettingsRequest request, int actorUserId, CancellationToken ct = default);

    /// <summary>Действующие настройки для служб каталога — с расшифрованным паролем.</summary>
    Task<Common.Options.LdapOptions?> GetEffectiveAsync(CancellationToken ct = default);

    /// <summary>Записать итог синхронизации, чтобы администратор видел, работает ли связь.</summary>
    Task RecordSyncAsync(int created, int updated, int deactivated, string? error, CancellationToken ct = default);
}

/// <summary>
/// Настройки интеграции со службой каталогов.
///
/// Значения из конфигурации сервера используются только как первоначальные: при
/// первом обращении они переносятся в базу, дальше правит администратор через
/// интерфейс. Иначе получилось бы два источника истины, и было бы неясно, какой
/// адрес каталога система использует на самом деле.
/// </summary>
public class DirectorySettingsService : IDirectorySettingsService
{
    private readonly DelosferaDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IAuditService _audit;
    private readonly Common.Options.LdapOptions? _configured;

    public DirectorySettingsService(
        DelosferaDbContext db,
        ISecretProtector protector,
        IAuditService audit,
        IOptions<Common.Options.LdapOptions> configured)
    {
        _db = db;
        _protector = protector;
        _audit = audit;
        _configured = string.IsNullOrWhiteSpace(configured.Value?.Server) ? null : configured.Value;
    }

    public async Task<DirectorySettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await LoadAsync(ct);

        return new DirectorySettingsDto
        {
            Enabled = settings.Enabled,
            Server = settings.Server,
            Port = settings.Port,
            UseSsl = settings.UseSsl,
            ServiceAccountLogin = settings.ServiceAccountLogin,
            HasPassword = !string.IsNullOrEmpty(settings.ServiceAccountPasswordEncrypted),
            UsersBaseDn = settings.UsersBaseDn,
            UsersFilter = settings.UsersFilter,
            PageSize = settings.PageSize,
            LoginAttribute = settings.LoginAttribute,
            EmailAttribute = settings.EmailAttribute,
            FullNameAttribute = settings.FullNameAttribute,
            // Пустое значение в настройке означало бы, что должность и отдел
            // перестали подтягиваться — держим умолчания Active Directory.
            PositionAttribute = Fallback(settings.PositionAttribute, "title"),
            OrgUnitAttribute = Fallback(settings.OrgUnitAttribute, "department"),
            SyncIntervalMinutes = settings.SyncIntervalMinutes,
            DefaultRoleId = settings.DefaultRoleId,
            LastSyncAt = settings.LastSyncAt,
            LastSyncCreated = settings.LastSyncCreated,
            LastSyncUpdated = settings.LastSyncUpdated,
            LastSyncDeactivated = settings.LastSyncDeactivated,
            LastSyncError = settings.LastSyncError,
        };
    }

    public async Task<DirectorySettingsDto> UpdateAsync(
        DirectorySettingsRequest request, int actorUserId, CancellationToken ct = default)
    {
        if (request.SyncIntervalMinutes is < 5 or > 1440)
            throw new InvalidOperationException(
                "Интервал синхронизации задаётся в пределах от 5 минут до суток");

        if (request.Port is < 1 or > 65535)
            throw new InvalidOperationException("Недопустимый номер порта");

        var settings = await LoadAsync(ct);

        settings.Enabled = request.Enabled;
        settings.Server = request.Server.Trim();
        settings.Port = request.Port;
        settings.UseSsl = request.UseSsl;
        settings.ServiceAccountLogin = request.ServiceAccountLogin.Trim();
        settings.UsersBaseDn = request.UsersBaseDn.Trim();
        settings.PageSize = request.PageSize is > 0 and <= 5000 ? request.PageSize : 500;
        settings.SyncIntervalMinutes = request.SyncIntervalMinutes;
        settings.DefaultRoleId = request.DefaultRoleId;

        if (!string.IsNullOrWhiteSpace(request.UsersFilter)) settings.UsersFilter = request.UsersFilter.Trim();
        if (!string.IsNullOrWhiteSpace(request.LoginAttribute)) settings.LoginAttribute = request.LoginAttribute.Trim();
        if (!string.IsNullOrWhiteSpace(request.EmailAttribute)) settings.EmailAttribute = request.EmailAttribute.Trim();
        if (!string.IsNullOrWhiteSpace(request.FullNameAttribute)) settings.FullNameAttribute = request.FullNameAttribute.Trim();
        if (!string.IsNullOrWhiteSpace(request.PositionAttribute)) settings.PositionAttribute = request.PositionAttribute.Trim();
        if (!string.IsNullOrWhiteSpace(request.OrgUnitAttribute)) settings.OrgUnitAttribute = request.OrgUnitAttribute.Trim();

        // Пустое поле пароля означает «не меняю»: иначе любое сохранение формы,
        // где пароль не показывается, стирало бы его.
        if (!string.IsNullOrEmpty(request.ServiceAccountPassword))
            settings.ServiceAccountPasswordEncrypted = _protector.Protect(request.ServiceAccountPassword);

        if (settings.Enabled)
        {
            if (string.IsNullOrWhiteSpace(settings.Server) || string.IsNullOrWhiteSpace(settings.UsersBaseDn))
                throw new InvalidOperationException(
                    "Для включения интеграции укажите адрес каталога и ветку поиска пользователей");

            if (string.IsNullOrEmpty(settings.ServiceAccountPasswordEncrypted))
                throw new InvalidOperationException(
                    "Для включения интеграции задайте пароль сервисной учётной записи");
        }

        await _db.SaveChangesAsync(ct);

        // В журнал идут параметры связи, но не пароль.
        await _audit.LogAsync("DirectorySettings", settings.Id, "Updated", actorUserId, new
        {
            settings.Enabled,
            settings.Server,
            settings.Port,
            settings.UseSsl,
            settings.UsersBaseDn,
            settings.SyncIntervalMinutes,
            passwordChanged = !string.IsNullOrEmpty(request.ServiceAccountPassword),
        });

        return await GetAsync(ct);
    }

    public async Task<Common.Options.LdapOptions?> GetEffectiveAsync(CancellationToken ct = default)
    {
        var settings = await LoadAsync(ct);

        if (!settings.Enabled
            || string.IsNullOrWhiteSpace(settings.Server)
            || string.IsNullOrWhiteSpace(settings.UsersBaseDn))
        {
            return null;
        }

        return new Common.Options.LdapOptions
        {
            Server = settings.Server,
            Port = settings.Port,
            UseSsl = settings.UseSsl,
            ServiceAccountLogin = settings.ServiceAccountLogin,
            ServiceAccountPassword = _protector.Unprotect(settings.ServiceAccountPasswordEncrypted),
            UsersBaseDn = settings.UsersBaseDn,
            UsersFilter = settings.UsersFilter,
            PageSize = settings.PageSize,
            LoginAttribute = settings.LoginAttribute,
            EmailAttribute = settings.EmailAttribute,
            FullNameAttribute = settings.FullNameAttribute,
            PositionAttribute = settings.PositionAttribute,
            OrgUnitAttribute = settings.OrgUnitAttribute,
            SyncIntervalMinutes = settings.SyncIntervalMinutes,
            DefaultRoleId = settings.DefaultRoleId,
            Enabled = settings.Enabled,
        };
    }

    private static string Fallback(string? value, string byDefault) =>
        string.IsNullOrWhiteSpace(value) ? byDefault : value.Trim();

    public async Task RecordSyncAsync(
        int created, int updated, int deactivated, string? error, CancellationToken ct = default)
    {
        var settings = await LoadAsync(ct);

        settings.LastSyncAt = DateTime.UtcNow;
        settings.LastSyncCreated = created;
        settings.LastSyncUpdated = updated;
        settings.LastSyncDeactivated = deactivated;
        settings.LastSyncError = error?.Length > 2000 ? error[..2000] : error;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Запись настроек. При первом обращении заполняется из конфигурации сервера —
    /// так уже развёрнутая система не теряет заданные при установке значения.
    /// </summary>
    private async Task<DirectorySettings> LoadAsync(CancellationToken ct)
    {
        var settings = await _db.DirectorySettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        settings = new DirectorySettings();

        if (_configured is not null)
        {
            settings.Enabled = _configured.Enabled;
            settings.Server = _configured.Server;
            settings.Port = _configured.Port;
            settings.UseSsl = _configured.UseSsl;
            settings.ServiceAccountLogin = _configured.ServiceAccountLogin ?? string.Empty;
            settings.UsersBaseDn = _configured.UsersBaseDn ?? string.Empty;
            settings.UsersFilter = _configured.UsersFilter;
            settings.PageSize = _configured.PageSize;
            settings.LoginAttribute = _configured.LoginAttribute;
            settings.EmailAttribute = _configured.EmailAttribute;
            settings.FullNameAttribute = _configured.FullNameAttribute;
            settings.PositionAttribute = _configured.PositionAttribute;
            settings.OrgUnitAttribute = _configured.OrgUnitAttribute;
            settings.SyncIntervalMinutes = _configured.SyncIntervalMinutes;
            settings.DefaultRoleId = _configured.DefaultRoleId;

            if (!string.IsNullOrEmpty(_configured.ServiceAccountPassword))
                settings.ServiceAccountPasswordEncrypted = _protector.Protect(_configured.ServiceAccountPassword);
        }

        _db.DirectorySettings.Add(settings);
        await _db.SaveChangesAsync(ct);

        return settings;
    }
}
