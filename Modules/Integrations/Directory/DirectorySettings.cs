using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Integrations.Directory;

/// <summary>
/// Настройки связи со службой каталогов: адрес, учётная запись для чтения,
/// ветка поиска и частота синхронизации.
///
/// Хранятся в базе, а не в конфигурации сервера: адрес контроллера домена и
/// пароль сервисной учётной записи меняет администратор системы, и такая правка
/// не должна требовать доступа к серверу и перезапуска. Пароль хранится
/// зашифрованным и наружу не отдаётся — в интерфейсе видно лишь, задан он или нет.
///
/// Запись одна на всю систему: каталог у банка один.
/// </summary>
public class DirectorySettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Синхронизация и доменный вход выключены, пока это не включено.</summary>
    public bool Enabled { get; set; }

    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 389;

    /// <summary>Шифрованное соединение (LDAPS). Требует доверия к сертификату каталога.</summary>
    public bool UseSsl { get; set; }

    /// <summary>Учётная запись для чтения каталога. Для домена — в виде user@domain.</summary>
    public string ServiceAccountLogin { get; set; } = string.Empty;

    /// <summary>Пароль сервисной учётной записи в зашифрованном виде.</summary>
    public string ServiceAccountPasswordEncrypted { get; set; } = string.Empty;

    /// <summary>Ветка каталога, откуда брать сотрудников.</summary>
    public string UsersBaseDn { get; set; } = string.Empty;

    public string UsersFilter { get; set; } = "(&(objectClass=user)(objectCategory=person))";
    public int PageSize { get; set; } = 500;

    public string LoginAttribute { get; set; } = "sAMAccountName";
    public string EmailAttribute { get; set; } = "mail";
    public string FullNameAttribute { get; set; } = "displayName";

    /// <summary>Как часто забирать пользователей из каталога.</summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>Роль, которую получают новые пользователи из каталога.</summary>
    public int? DefaultRoleId { get; set; }

    // ── Итог последней синхронизации: администратор должен видеть, работает ли связь ──

    public DateTime? LastSyncAt { get; set; }
    public int LastSyncCreated { get; set; }
    public int LastSyncUpdated { get; set; }
    public int LastSyncDeactivated { get; set; }

    /// <summary>Ошибка последней попытки; пусто, если прошла успешно.</summary>
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DirectorySettingsConfiguration : IEntityTypeConfiguration<DirectorySettings>
{
    public void Configure(EntityTypeBuilder<DirectorySettings> b)
    {
        b.ToTable("directory_settings");

        b.Property(x => x.Server).HasMaxLength(255);
        b.Property(x => x.ServiceAccountLogin).HasMaxLength(255);
        b.Property(x => x.ServiceAccountPasswordEncrypted).HasMaxLength(1024);
        b.Property(x => x.UsersBaseDn).HasMaxLength(512);
        b.Property(x => x.UsersFilter).HasMaxLength(512);
        b.Property(x => x.LoginAttribute).HasMaxLength(64);
        b.Property(x => x.EmailAttribute).HasMaxLength(64);
        b.Property(x => x.FullNameAttribute).HasMaxLength(64);
        b.Property(x => x.LastSyncError).HasMaxLength(2000);
    }
}
