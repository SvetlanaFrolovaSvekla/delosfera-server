using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class ActualizationNotificationSettingsConfiguration
    : IEntityTypeConfiguration<ActualizationNotificationSettings>
{
    public void Configure(EntityTypeBuilder<ActualizationNotificationSettings> b)
    {
        b.ToTable("vnd_actualization_notification_settings");

        // По умолчанию сводка выключена и вложение содержит только обязательные колонки —
        // администратор включает рассылку осознанно, см. ActualizationNotificationSettings.
        b.HasData(new
        {
            Id = 1,
            MonthlyDigestEnabled = false,
            MonthlyDigestColumnsCsv = "",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
