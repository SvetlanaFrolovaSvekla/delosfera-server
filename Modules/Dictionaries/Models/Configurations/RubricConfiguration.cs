using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.ToTable("dictionary_rubric");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1,  TitleRu = "Структурные подразделения Головного офиса", TitleEn = "Head Office Structural Units", TitleKg = "Башкы офистин түзүмдүк бөлүмдөрү", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2,  TitleRu = "Структурные подразделения филиалов", TitleEn = "Branch Structural Units", TitleKg = "Филиалдардын түзүмдүк бөлүмдөрү", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 3,  TitleRu = "Безопасность", TitleEn = "Security", TitleKg = "Коопсуздук", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4,  TitleRu = "Информационные технологии", TitleEn = "Information Technology", TitleKg = "Маалыматтык технологиялар", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5,  TitleRu = "Управление рисками", TitleEn = "Risk Management", TitleKg = "Тобокелдиктерди башкаруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6,  TitleRu = "Документооборот и делопроизводство", TitleEn = "Document Management and Records Keeping", TitleKg = "Документ жүгүртүү жана иш кагаздарын жүргүзүү", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7,  TitleRu = "Управление персоналом", TitleEn = "Human Resources Management", TitleKg = "Кадрларды башкаруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 8,  TitleRu = "Кредитная деятельность", TitleEn = "Lending Activity", TitleKg = "Кредиттик иш-аракет", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 9,  TitleRu = "Розничное кредитование", TitleEn = "Retail Lending", TitleKg = "Чекене кредиттөө", ParentId = (int?)8, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 10, TitleRu = "Корпоративное кредитование", TitleEn = "Corporate Lending", TitleKg = "Корпоративдик кредиттөө", ParentId = (int?)8, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 11, TitleRu = "Депозиты и расчетно-кассовое обслуживание", TitleEn = "Deposits and Cash Settlement Services", TitleKg = "Депозиттер жана эсептешүү-кассалык тейлөө", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 12, TitleRu = "Депозитные операции", TitleEn = "Deposit Operations", TitleKg = "Депозиттик операциялар", ParentId = (int?)11, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 13, TitleRu = "Расчетно-кассовое обслуживание", TitleEn = "Cash Settlement Services", TitleKg = "Эсептешүү-кассалык тейлөө", ParentId = (int?)11, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 14, TitleRu = "Платежные карты и БСО", TitleEn = "Payment Cards and Strict Reporting Forms", TitleKg = "Төлөм карталары жана катуу отчеттуулук бланктары", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 15, TitleRu = "Бухгалтерский учет и отчетность", TitleEn = "Accounting and Reporting", TitleKg = "Эсепке алуу жана отчеттуулук", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 16, TitleRu = "Маркетинг и PR", TitleEn = "Marketing and PR", TitleKg = "Маркетинг жана PR", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}