using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Нормативы сроков согласования редакции ВНД по умолчанию — значения, которыми
/// предзаполняется маршрут согласования ("Первичное согласование", "Согласование после
/// внесённых изменений", "Финальная выдержка") в модалке запуска согласования.
/// Настраиваются администратором в справочнике "Нормативы согласования по умолчанию"
/// (раздел ВНД в Справочниках). Инициатор по-прежнему может изменить их для конкретного
/// маршрута — справочник задаёт только стартовые значения.
///
/// Запись одна на всю систему. Значения хранятся в РАБОЧИХ минутах — так же, как нормативы в
/// самом процессе согласования (StartApprovalRequest.*DeadlineMinutes): 1 д. = 1 рабочий
/// день банка (по умолчанию 9 ч = 540 мин, задаётся в справочнике "Производственный календарь";
/// при смене рабочего времени эти значения пересчитываются пропорционально — см.
/// VndWorkCalendarService.UpdateHoursAsync).
/// </summary>
public class VndApprovalNormSettings : IAuditableEntity
{
    public const int DefaultPrimaryMinutes = 7 * Services.VndWorkingCalendar.DefaultWorkDayMinutes;
    public const int DefaultRepeatMinutes = 4 * Services.VndWorkingCalendar.DefaultWorkDayMinutes;
    public const int DefaultFinalHoldMinutes = 3 * Services.VndWorkingCalendar.DefaultWorkDayMinutes;

    public int Id { get; set; }

    /// <summary>"Первичное согласование", рабочие минуты. По умолчанию 7 рабочих дней.</summary>
    public int PrimaryDeadlineMinutes { get; set; } = DefaultPrimaryMinutes;

    /// <summary>"Согласование после внесённых изменений", рабочие минуты. По умолчанию 4 рабочих дня.</summary>
    public int RepeatDeadlineMinutes { get; set; } = DefaultRepeatMinutes;

    /// <summary>"Финальная выдержка", рабочие минуты. По умолчанию 3 рабочих дня.</summary>
    public int FinalHoldDeadlineMinutes { get; set; } = DefaultFinalHoldMinutes;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VndApprovalNormSettingsConfiguration : IEntityTypeConfiguration<VndApprovalNormSettings>
{
    public void Configure(EntityTypeBuilder<VndApprovalNormSettings> b)
    {
        b.ToTable("vnd_approval_norm_settings");

        b.HasData(new VndApprovalNormSettings
        {
            Id = 1,
            // Литералы, а не Default*Minutes: засеянные данные зафиксированы миграцией
            // (AddVndApprovalNormSettings → ConvertVndApprovalToWorkingTime), константы могут меняться.
            PrimaryDeadlineMinutes = 7 * 540,
            RepeatDeadlineMinutes = 4 * 540,
            FinalHoldDeadlineMinutes = 3 * 540,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
