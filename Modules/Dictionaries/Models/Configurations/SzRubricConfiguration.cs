using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class SzRubricConfiguration : IEntityTypeConfiguration<SzRubric>
{
    public void Configure(EntityTypeBuilder<SzRubric> builder)
    {
        builder.ToTable("dictionary_sz_rubric");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Плейсхолдер-значения: реального рубрикатора СЗ ещё нет, это просто несколько
        // произвольных пунктов, чтобы список не был пустым — реальные значения вводятся
        // через справочник "Рубрикатор СЗ" (как и для Рубрикатора ВНД).
        builder.HasData(
            new { Id = 1, TitleRu = "Организационно-распорядительные вопросы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Приказы и распоряжения", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)1, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "Регламенты и положения", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)1, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Кадровые вопросы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Отпуска и командировки", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)4, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Финансово-хозяйственные вопросы", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Прочее", TitleEn = (string?)null, TitleKg = (string?)null, ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}
