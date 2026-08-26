using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Integrations.Mail;

/// <summary>
/// Почтовые уведомления: слать письма или нет и куда.
///
/// Хранятся в базе, а не в конфигурации сервера — как и настройки службы
/// каталогов. Выключить рассылку должен уметь администратор системы: во время
/// обкатки письма о проверочных записках уходят настоящим людям, и ждать
/// доступа к серверу ради одного выключателя неправильно.
///
/// Конфигурация сервера (раздел Mail) остаётся первоначальным значением:
/// при первом запуске настройки берутся оттуда, дальше живут в базе.
///
/// Внутрисистемные уведомления — колокольчик — этим выключателем не гасятся.
/// На них держатся задачи: погасив их, человек перестанет видеть, что от него
/// чего-то ждут, и решит, что система сломалась.
/// </summary>
public class MailSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Выключено — письма не уходят и в очередь не ставятся.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }

    /// <summary>Учётная запись для отправки. Пусто — сервер принимает без пароля.</summary>
    public string? User { get; set; }

    /// <summary>Пароль в зашифрованном виде. Наружу не отдаётся.</summary>
    public string? PasswordEncrypted { get; set; }

    public string FromAddress { get; set; } = "sed@keremetbank.kg";
    public string FromName { get; set; } = "СЭД Керемет Банк";

    /// <summary>Адрес системы для ссылок в письмах: без него ссылка ведёт в никуда.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Сколько раз пробовать отправить, прежде чем считать письмо потерянным.</summary>
    public int MaxAttempts { get; set; } = 5;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MailSettingsConfiguration : IEntityTypeConfiguration<MailSettings>
{
    public void Configure(EntityTypeBuilder<MailSettings> b)
    {
        b.ToTable("mail_settings");

        b.Property(x => x.Host).HasMaxLength(255);
        b.Property(x => x.User).HasMaxLength(255);
        b.Property(x => x.PasswordEncrypted).HasMaxLength(1024);
        b.Property(x => x.FromAddress).HasMaxLength(255);
        b.Property(x => x.FromName).HasMaxLength(255);
        b.Property(x => x.BaseUrl).HasMaxLength(512);
    }
}
