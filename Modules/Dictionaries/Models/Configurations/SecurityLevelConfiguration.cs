using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class SecurityLevelConfiguration : IEntityTypeConfiguration<SecurityLevel>
{
    public void Configure(EntityTypeBuilder<SecurityLevel> builder)
    {
        builder.ToTable("dictionary_security_level");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new SecurityLevel { Id = 1, TitleRu = "Открытый доступ", TitleEn = "Public Access", TitleKg = "Ачык жеткиликтүүлүк", CreatedAt = seedDate, UpdatedAt = seedDate },
            new SecurityLevel { Id = 2, TitleRu = "Конфиденциально", TitleEn = "Confidential", TitleKg = "Конфиденциалдуу", CreatedAt = seedDate, UpdatedAt = seedDate },
            new SecurityLevel { Id = 3, TitleRu = "Секретно", TitleEn = "Secret", TitleKg = "Жашыруун", CreatedAt = seedDate, UpdatedAt = seedDate },
            new SecurityLevel { Id = 4, TitleRu = "Совершенно секретно", TitleEn = "Top Secret", TitleKg = "Өтө жашыруун", CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}