using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Settings.Models.Configurations;

public class SettingsChangeConfiguration : IEntityTypeConfiguration<SettingsChange>
{
    public void Configure(EntityTypeBuilder<SettingsChange> builder)
    {
        builder.ToTable("settings_change");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Area).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityTitle).HasMaxLength(300);
        builder.Property(x => x.ChangesJson).HasColumnType("jsonb");

        builder.Property(x => x.UserName).HasMaxLength(300);

        // Внешнего ключа на пользователя нет намеренно — см. пояснение в модели.
        // Журнал переживает удаление учётной записи и не роняет сохранение
        // справочника, если номер в токене не совпал ни с кем в базе.

        // Журнал листают с конца и отбирают по области — под это и индексы.
        builder.HasIndex(x => x.At);
        builder.HasIndex(x => new { x.Area, x.At });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
