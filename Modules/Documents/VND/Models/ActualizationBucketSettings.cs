using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Пороги индикации сроков актуализации ВНД (Normal/Approaching/Critical, см.
/// ActualizationBucket) — настраиваются администратором в справочнике "Пороги
/// индикации сроков актуализации" (раздел ВНД в Справочниках).
///
/// Запись одна на всю систему. "Просрочено" (ActualizationBucket.Overdue) этими
/// порогами не управляется: это всегда дата актуализации в прошлом, отдельного
/// порога у неё нет и быть не может.
/// </summary>
public class ActualizationBucketSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Ближе этого числа дней до срока актуализации статус "Критично".</summary>
    public int CriticalDays { get; set; } = 5;

    /// <summary>До этого числа дней статус "Приближается", дальше — "Норма".</summary>
    public int ApproachingDays { get; set; } = 30;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ActualizationBucketSettingsConfiguration : IEntityTypeConfiguration<ActualizationBucketSettings>
{
    public void Configure(EntityTypeBuilder<ActualizationBucketSettings> b)
    {
        b.ToTable("vnd_actualization_bucket_settings");

        // Значения по умолчанию совпадают с прежними захардкоженными порогами
        // (ActualizationThresholds): критично — 5 дней, приближается — 30 дней.
        b.HasData(new ActualizationBucketSettings
        {
            Id = 1,
            CriticalDays = 5,
            ApproachingDays = 30,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
