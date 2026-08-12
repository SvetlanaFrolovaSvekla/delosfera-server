using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Пороги индикации и сроки напоминаний по плану актуализации (PLN-03, PLN-04).
///
/// Хранятся в базе, а не в конфигурации приложения: по ТЗ их задаёт Отдел
/// методологии, а не администратор сервера, и правка не должна требовать
/// перезапуска системы.
///
/// Запись одна на всю систему — пороги общие для всех подразделений.
/// </summary>
public class ActualizationSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>До этого числа дней срок считается спокойным (зелёный).</summary>
    public int GreenThresholdDays { get; set; } = 30;

    /// <summary>Ближе этого числа дней срок критический (красный).</summary>
    public int RedThresholdDays { get; set; } = 5;

    /// <summary>За сколько дней уходит критическое напоминание с копией куратору.</summary>
    public int CriticalReminderDays { get; set; } = 5;

    /// <summary>Рассылать ли ежемесячную сводку 1-го числа.</summary>
    public bool MonthlyDigestEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ActualizationSettingsConfiguration : IEntityTypeConfiguration<ActualizationSettings>
{
    public void Configure(EntityTypeBuilder<ActualizationSettings> b)
    {
        b.ToTable("vnd_actualization_settings");

        // Значения по умолчанию из ТЗ: зелёный — более 30 дней, красный — менее 5.
        b.HasData(new ActualizationSettings
        {
            Id = 1,
            GreenThresholdDays = 30,
            RedThresholdDays = 5,
            CriticalReminderDays = 5,
            MonthlyDigestEnabled = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
