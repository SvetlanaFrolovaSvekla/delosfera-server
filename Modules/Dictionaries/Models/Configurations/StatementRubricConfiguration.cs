using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class StatementRubricConfiguration : IEntityTypeConfiguration<StatementRubric>
{
    public void Configure(EntityTypeBuilder<StatementRubric> builder)
    {
        builder.ToTable("dictionary_statement_rubric");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Стартовые значения, чтобы список не был пустым. Реальный рубрикатор заявлений
        // ведётся через справочник "Рубрикатор заявлений" — эти пункты можно править и удалять.
        builder.HasData(
            new { Id = 1, TitleRu = "Кадровые вопросы", TitleEn = (string?)"HR matters", TitleKg = (string?)"Кадрдык маселелер", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Отпуска", TitleEn = (string?)"Leave", TitleKg = (string?)"Өргүүлөр", ParentId = (int?)1, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "Приём, перевод, увольнение", TitleEn = (string?)"Hiring, transfer, dismissal", TitleKg = (string?)"Ишке алуу, которуу, бошотуу", ParentId = (int?)1, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Справки и копии документов", TitleEn = (string?)"Certificates and document copies", TitleKg = (string?)"Маалым каттар жана документтердин көчүрмөлөрү", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Финансовые вопросы", TitleEn = (string?)"Financial matters", TitleKg = (string?)"Финансылык маселелер", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Материальная помощь", TitleEn = (string?)"Financial assistance", TitleKg = (string?)"Материалдык жардам", ParentId = (int?)5, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Прочее", TitleEn = (string?)"Other", TitleKg = (string?)"Башка", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}
