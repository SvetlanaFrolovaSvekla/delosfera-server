using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("dictionary_keyword");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1, TitleRu = "Идентификация", TitleEn = "Identification", TitleKg = "Идентификациялоо", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Анализ", TitleEn = "Analysis", TitleKg = "Анализ", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 3, TitleRu = "Сверка", TitleEn = "Reconciliation", TitleKg = "Салыштыруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Сверка остатков", TitleEn = "Balance Reconciliation", TitleKg = "Калдыктарды салыштыруу", ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Сверка данных", TitleEn = "Data Reconciliation", TitleKg = "Маалыматтарды салыштыруу", ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 6, TitleRu = "Ответственность", TitleEn = "Responsibility", TitleKg = "Жоопкерчилик", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Бюджет", TitleEn = "Budget", TitleKg = "Бюджет", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 8, TitleRu = "Расход", TitleEn = "Expense", TitleKg = "Чыгым", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 9, TitleRu = "Расход материальный", TitleEn = "Material Expense", TitleKg = "Материалдык чыгым", ParentId = (int?)8, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 10, TitleRu = "Расход операционный", TitleEn = "Operational Expense", TitleKg = "Операциялык чыгым", ParentId = (int?)8, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 11, TitleRu = "Безопасность", TitleEn = "Security", TitleKg = "Коопсуздук", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 12, TitleRu = "Матрица", TitleEn = "Matrix", TitleKg = "Матрица", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 13, TitleRu = "План", TitleEn = "Plan", TitleKg = "План", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 14, TitleRu = "План-график", TitleEn = "Schedule", TitleKg = "План-график", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 15, TitleRu = "План мероприятий", TitleEn = "Action Plan", TitleKg = "Иш-чаралар планы", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 16, TitleRu = "Формирование", TitleEn = "Formation", TitleKg = "Түзүү", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 17, TitleRu = "Резервный сервер", TitleEn = "Backup Server", TitleKg = "Резервдик сервер", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 18, TitleRu = "Автоматизация", TitleEn = "Automation", TitleKg = "Автоматташтыруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 19, TitleRu = "Непрерывность", TitleEn = "Continuity", TitleKg = "Үзгүлтүксүздүк", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}