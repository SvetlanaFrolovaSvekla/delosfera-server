using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class ApprovalBodyConfiguration : IEntityTypeConfiguration<ApprovalBody>
{
    public void Configure(EntityTypeBuilder<ApprovalBody> builder)
    {
        builder.ToTable("dictionary_approval_body");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict); // нельзя удалить родителя, пока есть дети

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1, TitleRu = "Комитет по управлению активами и пассивами", TitleEn = "Asset and Liability Management Committee", TitleKg = "Активдерди жана пассивдерди башкаруу комитети", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Общее собрание акционеров", TitleEn = "General Meeting of Shareholders", TitleKg = "Акционерлердин жалпы жыйыны", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "Правление", TitleEn = "Management Board", TitleKg = "Башкарма", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, TitleRu = "Заместитель Председателя Правления", TitleEn = "Deputy Chairman of the Management Board", TitleKg = "Башкарманын төрагасынын орун басары", ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, TitleRu = "Председатель Правления", TitleEn = "Chairman of the Management Board", TitleKg = "Башкарманын төрагасы", ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, TitleRu = "Член Правления", TitleEn = "Member of the Management Board", TitleKg = "Башкарманын мүчөсү", ParentId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 7, TitleRu = "Совет директоров", TitleEn = "Board of Directors", TitleKg = "Директорлор кеңеши", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 8, TitleRu = "Тарифный комитет", TitleEn = "Tariff Committee", TitleKg = "Тарифтик комитет", ParentId = (int?)null, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}