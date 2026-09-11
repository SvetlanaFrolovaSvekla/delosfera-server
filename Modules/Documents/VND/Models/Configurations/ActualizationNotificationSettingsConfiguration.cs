using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class ActualizationNotificationSettingsConfiguration
    : IEntityTypeConfiguration<ActualizationNotificationSettings>
{
    public void Configure(EntityTypeBuilder<ActualizationNotificationSettings> b)
    {
        b.ToTable("vnd_actualization_notification_settings");

        // По умолчанию обе рассылки выключены и критических порогов не задано — администратор
        // включает их осознанно, см. ActualizationNotificationSettings.
        b.HasData(new
        {
            Id = 1,
            MonthlyDigestEnabled = false,
            MonthlyDigestColumnsCsv = "",
            CriticalRemindersEnabled = false,
            CriticalReminderDaysCsv = "",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
