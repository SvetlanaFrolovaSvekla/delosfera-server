using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Рассылка «Новые редакции ВНД» — раздел «Рассылка с уведомлением о новых редакциях» на
/// странице «Настройки рассылок по актуализации ВНД». Письмо уходит выбранным сотрудникам
/// каждый раз, когда ВНД консолидируется (становится действующей новая редакция) или у нового
/// ВНД появляется первая действующая редакция. Запись одна на всю систему.
///
/// Отдельная от ActualizationNotificationSettings таблица намеренно: те настройки сохраняются
/// одним PUT целиком со всех разделов, и новые поля в них затирались бы разделами, которые о них
/// не знают.
/// </summary>
public class VndNewRedactionMailingSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Включена ли рассылка. По умолчанию выключена — администратор включает её
    /// осознанно, после того как выбрал получателей.</summary>
    public bool Enabled { get; set; }

    /// <summary>Показывать ли уведомление внутри Делосферы (в дополнение к письму).</summary>
    public bool NotifyInApp { get; set; }

    /// <summary>Отправлять ли письмо на почту. По умолчанию да — ради этого рассылка и заведена.</summary>
    public bool NotifyEmail { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Получатель рассылки «Новые редакции ВНД». Одна строка на пользователя.</summary>
public class VndNewRedactionMailingRecipient
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class VndNewRedactionMailingSettingsConfiguration : IEntityTypeConfiguration<VndNewRedactionMailingSettings>
{
    public void Configure(EntityTypeBuilder<VndNewRedactionMailingSettings> b)
    {
        b.ToTable("vnd_new_redaction_mailing_settings");
    }
}

public class VndNewRedactionMailingRecipientConfiguration : IEntityTypeConfiguration<VndNewRedactionMailingRecipient>
{
    public void Configure(EntityTypeBuilder<VndNewRedactionMailingRecipient> b)
    {
        b.ToTable("vnd_new_redaction_mailing_recipient");
        b.HasKey(x => x.UserId);

        // Удалили пользователя — он пропадает и из рассылки.
        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
