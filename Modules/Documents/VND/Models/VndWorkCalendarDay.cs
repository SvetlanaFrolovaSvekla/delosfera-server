using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Праздничный (нерабочий) день справочника "Производственный календарь" раздела ВНД — главный
/// редактор проставляет их на каждый год. Используется ТОЛЬКО при расчёте сроков согласования
/// редакций ВНД (см. VndWorkingCalendar): срок идёт только в рабочее время банка — пн–пт в
/// рабочие часы (<see cref="VndWorkHoursSettings"/>), без праздников. Одна запись на дату.
/// </summary>
public class VndWorkCalendarDay : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Календарная дата по времени банка (Asia/Bishkek).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Название: "Нооруз", "День независимости" и т.п.</summary>
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VndWorkCalendarDayConfiguration : IEntityTypeConfiguration<VndWorkCalendarDay>
{
    public void Configure(EntityTypeBuilder<VndWorkCalendarDay> b)
    {
        b.ToTable("vnd_work_calendar_day");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Date).IsUnique();
    }
}

/// <summary>
/// Рабочее время банка для сроков согласования ВНД (справочник "Производственный календарь").
/// Запись одна на всю систему; время — минуты от полуночи по Бишкеку. Рабочие дни — пн–пт.
/// Длина рабочего дня (конец − начало) — это «1 д.» в нормативах согласования.
/// </summary>
public class VndWorkHoursSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Начало рабочего дня, минут от полуночи. По умолчанию 09:00 (540).</summary>
    public int WorkStartMinutes { get; set; } = 9 * 60;

    /// <summary>Конец рабочего дня, минут от полуночи. По умолчанию 18:00 (1080).</summary>
    public int WorkEndMinutes { get; set; } = 18 * 60;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VndWorkHoursSettingsConfiguration : IEntityTypeConfiguration<VndWorkHoursSettings>
{
    public void Configure(EntityTypeBuilder<VndWorkHoursSettings> b)
    {
        b.ToTable("vnd_work_hours_settings");

        b.HasData(new VndWorkHoursSettings
        {
            Id = 1,
            WorkStartMinutes = 9 * 60,
            WorkEndMinutes = 18 * 60,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
