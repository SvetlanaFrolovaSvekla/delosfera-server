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
/// Запись одна на всю систему. Значения хранятся в минутах — так же, как нормативы в
/// самом процессе согласования (StartApprovalRequest.*DeadlineMinutes).
/// </summary>
public class VndApprovalNormSettings : IAuditableEntity
{
    public const int DefaultPrimaryMinutes = 7 * 24 * 60;
    public const int DefaultRepeatMinutes = 4 * 24 * 60;
    public const int DefaultFinalHoldMinutes = 3 * 24 * 60;

    public int Id { get; set; }

    /// <summary>"Первичное согласование", минуты. По умолчанию 7 дней.</summary>
    public int PrimaryDeadlineMinutes { get; set; } = DefaultPrimaryMinutes;

    /// <summary>"Согласование после внесённых изменений", минуты. По умолчанию 4 дня.</summary>
    public int RepeatDeadlineMinutes { get; set; } = DefaultRepeatMinutes;

    /// <summary>"Финальная выдержка", минуты. По умолчанию 3 дня.</summary>
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
            PrimaryDeadlineMinutes = VndApprovalNormSettings.DefaultPrimaryMinutes,
            RepeatDeadlineMinutes = VndApprovalNormSettings.DefaultRepeatMinutes,
            FinalHoldDeadlineMinutes = VndApprovalNormSettings.DefaultFinalHoldMinutes,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
