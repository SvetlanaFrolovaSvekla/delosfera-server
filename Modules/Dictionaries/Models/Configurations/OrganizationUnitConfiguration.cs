using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> builder)
    {
        builder.ToTable("dictionary_organization_unit");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.HeadUser)
            .WithMany()
            .HasForeignKey(x => x.HeadUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CuratorUser)
            .WithMany()
            .HasForeignKey(x => x.CuratorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1, TitleRu = "Управление делами", TitleEn = "Administrative Affairs Department", TitleKg = "Иштерди башкаруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Начальник УД", TitleEn = "Head of Administrative Affairs Department", TitleKg = "ИБ башчысы", ParentId = (int?)1, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 3, TitleRu = "Управление информационных технологий", TitleEn = "Information Technology Department", TitleKg = "Маалыматтык технологиялар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Управление казначейских операций", TitleEn = "Treasury Operations Department", TitleKg = "Казыналык операциялар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 5, TitleRu = "Управление комплаенс контроля", TitleEn = "Compliance Control Department", TitleKg = "Комплаенс контролдоо башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Отдел комплаенс контроля", TitleEn = "Compliance Control Division", TitleKg = "Комплаенс контролдоо бөлүмү", ParentId = (int?)5, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Отдел контроля качества", TitleEn = "Quality Control Division", TitleKg = "Сапатты контролдоо бөлүмү", ParentId = (int?)5, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 8, TitleRu = "Управление кредитования", TitleEn = "Lending Department", TitleKg = "Кредиттөө башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 10, TitleRu = "Управление маркетинга", TitleEn = "Marketing Department", TitleKg = "Маркетинг башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 11, TitleRu = "Управление обеспечения кредитов", TitleEn = "Loan Collateral Department", TitleKg = "Кредиттерди камсыздоо башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 12, TitleRu = "Управление обеспечения кредитов (УОК)", TitleEn = "Loan Collateral Department (LCD)", TitleKg = "Кредиттерди камсыздоо башкармасы (ККБ)", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 13, TitleRu = "Управление платежных сервисов", TitleEn = "Payment Services Department", TitleKg = "Төлөм сервистери башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 14, TitleRu = "Отдел претензионной работы", TitleEn = "Claims Handling Division", TitleKg = "Дооматтык иштер бөлүмү", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 15, TitleRu = "Отдел учета платежных систем", TitleEn = "Payment Systems Accounting Division", TitleKg = "Төлөм системаларын эсепке алуу бөлүмү", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 16, TitleRu = "Отдел эквайринга", TitleEn = "Acquiring Division", TitleKg = "Эквайринг бөлүмү", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 17, TitleRu = "Отдел эмиссии", TitleEn = "Card Issuance Division", TitleKg = "Эмиссия бөлүмү", ParentId = (int?)13, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 18, TitleRu = "Управление по взаимоотношениям с клиентами", TitleEn = "Customer Relations Department", TitleKg = "Кардарлар менен мамилелер башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 19, TitleRu = "Управление по работе с проблемными кредитами", TitleEn = "Problem Loans Department", TitleKg = "Көйгөйлүү кредиттер менен иштөө башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 20, TitleRu = "Управление по работе с прочей собственностью", TitleEn = "Other Property Management Department", TitleKg = "Башка мүлк менен иштөө башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 21, TitleRu = "Управление поддержки бизнеса", TitleEn = "Business Support Department", TitleKg = "Бизнести колдоо башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 22, TitleRu = "Управление продаж", TitleEn = "Sales Department", TitleKg = "Сатуулар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 23, TitleRu = "Отдел операционного сопровождения", TitleEn = "Operational Support Division", TitleKg = "Операциялык коштоо бөлүмү", ParentId = (int?)22, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 24, TitleRu = "Отдел регионального сопровождения", TitleEn = "Regional Support Division", TitleKg = "Аймактык коштоо бөлүмү", ParentId = (int?)22, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 25, TitleRu = "Управление продаж МСБ", TitleEn = "SME Sales Department", TitleKg = "ЧОБ сатуулар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 26, TitleRu = "Управление рисками", TitleEn = "Risk Management Department", TitleKg = "Тобокелдиктерди башкаруу башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 27, TitleRu = "Сектор операционных рисков УР", TitleEn = "Operational Risk Sector", TitleKg = "ТБ операциялык тобокелдиктер секторуу", ParentId = (int?)26, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 28, TitleRu = "Управление риск-менеджмента", TitleEn = "Risk Management Office", TitleKg = "Тобокелдик-менеджмент башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 29, TitleRu = "Управление розничных продаж", TitleEn = "Retail Sales Department", TitleKg = "Чекене сатуулар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 30, TitleRu = "Управление розничных продуктов", TitleEn = "Retail Products Department", TitleKg = "Чекене продукттар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 31, TitleRu = "Управление стратегического планирования и бюджетирования", TitleEn = "Strategic Planning and Budgeting Department", TitleKg = "Стратегиялык пландоо жана бюджеттөө башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 32, TitleRu = "Управление человеческими ресурсами", TitleEn = "Human Resources Department", TitleKg = "Адам ресурстарын башкаруу", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 33, TitleRu = "Управление методологии и продуктов", TitleEn = "Methodology and Products Department", TitleKg = "Методология жана продукттар башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 34, TitleRu = "Юридическое управление", TitleEn = "Legal Department", TitleKg = "Юридикалык башкарма", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },

            new { Id = 35, TitleRu = "Административный отдел", TitleEn = "Administrative Division", TitleKg = "Административдик бөлүм", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 36, TitleRu = "Правление", TitleEn = "Management Board", TitleKg = "Башкарма", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 37, TitleRu = "Канцелярия", TitleEn = "Chancellery", TitleKg = "Канцелярия", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 38, TitleRu = "УБУиО", TitleEn = "Accounting and Reporting Department", TitleKg = "ЭБжО башкармасы", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 39, TitleRu = "Департамент безопасности", TitleEn = "Security Department", TitleKg = "Коопсуздук департаменти", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}