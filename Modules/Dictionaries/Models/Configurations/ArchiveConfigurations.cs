using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class StorageTermConfiguration : IEntityTypeConfiguration<StorageTerm>
{
    public void Configure(EntityTypeBuilder<StorageTerm> b)
    {
        b.ToTable("dictionary_storage_term");
        b.HasIndex(x => x.Code).IsUnique();

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Типовые сроки из инструкции по делопроизводству. Какие именно применяются
        // к служебным запискам — открытый вопрос В-2, справочник пополняем администратором.
        b.HasData(
            new { Id = 1, Code = "1г", TitleRu = "1 год", TitleEn = "1 year", TitleKg = "1 жыл", Years = (int?)1, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, Code = "3г", TitleRu = "3 года", TitleEn = "3 years", TitleKg = "3 жыл", Years = (int?)3, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, Code = "5л", TitleRu = "5 лет", TitleEn = "5 years", TitleKg = "5 жыл", Years = (int?)5, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, Code = "10л", TitleRu = "10 лет", TitleEn = "10 years", TitleKg = "10 жыл", Years = (int?)10, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, Code = "75л", TitleRu = "75 лет (по личному составу)", TitleEn = "75 years (personnel)", TitleKg = "75 жыл (кадрлар боюнча)", Years = (int?)75, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 6, Code = "Пост", TitleRu = "Постоянно", TitleEn = "Permanent", TitleKg = "Туруктуу", Years = (int?)null, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}

public class NomenclatureCaseConfiguration : IEntityTypeConfiguration<NomenclatureCase>
{
    public void Configure(EntityTypeBuilder<NomenclatureCase> b)
    {
        b.ToTable("dictionary_nomenclature_case");

        // Номенклатура годовая: один и тот же индекс заводится заново на каждый год.
        b.HasIndex(x => new { x.Year, x.Index }).IsUnique();

        b.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.StorageTerm)
            .WithMany()
            .HasForeignKey(x => x.StorageTermId)
            .OnDelete(DeleteBehavior.Restrict);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Стартовые дела для служебных записок: кадровые хранятся дольше остальных.
        b.HasData(
            new { Id = 1, Index = "05-01", TitleRu = "Служебные записки по основной деятельности", TitleEn = "Memos on core activities", TitleKg = "Негизги ишмердүүлүк боюнча кызматтык каттар", Year = 2026, OrgUnitId = (int?)1, StorageTermId = (int?)3, ClosedOn = (DateOnly?)null, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, Index = "05-02", TitleRu = "Служебные записки по личному составу", TitleEn = "Memos on personnel", TitleKg = "Кадрлар боюнча кызматтык каттар", Year = 2026, OrgUnitId = (int?)1, StorageTermId = (int?)5, ClosedOn = (DateOnly?)null, IsActive = true, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}
