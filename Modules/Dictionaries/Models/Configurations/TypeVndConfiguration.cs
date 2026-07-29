using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class TypeVndConfiguration : IEntityTypeConfiguration<TypeVnd>
{
    public void Configure(EntityTypeBuilder<TypeVnd> builder)
    {
        builder.ToTable("dictionary_type_vnd");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new TypeVnd { Id = 1, TitleRu = "Базовые условия кредитного продукта", TitleEn = "Basic Terms of Credit Product", TitleKg = "Кредиттик продукттун негизги шарттары", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 2, TitleRu = "Договор", TitleEn = "Agreement", TitleKg = "Келишим", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 3, TitleRu = "Должностная инструкция", TitleEn = "Job Description", TitleKg = "Кызматтык нускама", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 4, TitleRu = "Инструкция", TitleEn = "Instruction", TitleKg = "Нускама", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 5, TitleRu = "Кодекс", TitleEn = "Code", TitleKg = "Кодекс", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 6, TitleRu = "Концепция", TitleEn = "Concept", TitleKg = "Концепция", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 7, TitleRu = "Лимиты", TitleEn = "Limits", TitleKg = "Лимиттер", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 8, TitleRu = "Матрица", TitleEn = "Matrix", TitleKg = "Матрица", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 9, TitleRu = "Методика", TitleEn = "Methodology", TitleKg = "Методика", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 10, TitleRu = "План", TitleEn = "Plan", TitleKg = "План", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 11, TitleRu = "Политика", TitleEn = "Policy", TitleKg = "Саясат", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 12, TitleRu = "Положение", TitleEn = "Regulation", TitleKg = "Жобо", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 13, TitleRu = "Порядок", TitleEn = "Procedure", TitleKg = "Тартип", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 14, TitleRu = "Правила", TitleEn = "Rules", TitleKg = "Эрежелер", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 15, TitleRu = "Программа", TitleEn = "Program", TitleKg = "Программа", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 16, TitleRu = "Процедура", TitleEn = "Process", TitleKg = "Жараян", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 17, TitleRu = "Регламент", TitleEn = "Regulations", TitleKg = "Регламент", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 18, TitleRu = "Руководство", TitleEn = "Manual", TitleKg = "Жетекчилик", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 19, TitleRu = "Система", TitleEn = "System", TitleKg = "Система", CreatedAt = seedDate, UpdatedAt = seedDate },
            new TypeVnd { Id = 20, TitleRu = "Устав", TitleEn = "Charter", TitleKg = "Устав", CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}