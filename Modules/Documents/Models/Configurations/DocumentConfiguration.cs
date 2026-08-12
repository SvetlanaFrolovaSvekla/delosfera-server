using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.Models.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("document");

        // Поисковый вектор считает сама база (GENERATED ... STORED): отдельная
        // синхронизация индекса рано или поздно расходится с данными, а генерируемая
        // колонка не может устареть. Словарь задан константой — иначе выражение не
        // immutable и Postgres не примет его в вычисляемую колонку.
        builder.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(title, '') || ' ' || coalesce(reg_number, ''))",
                stored: true);

        builder.HasIndex(x => x.SearchVector).HasMethod("GIN");

        builder.Property(x => x.Type).HasConversion<string>();
        builder.Property(x => x.FieldValues).HasColumnType("jsonb");

        builder.HasOne(x => x.Definition)
            .WithMany()
            .HasForeignKey(x => x.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Дело и срок хранения — справочные значения, удалять их вместе с документом нельзя.
        builder.HasOne(x => x.NomenclatureCase)
            .WithMany()
            .HasForeignKey(x => x.NomenclatureCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StorageTerm)
            .WithMany()
            .HasForeignKey(x => x.StorageTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Type, x.RegNumber });
        builder.HasIndex(x => x.StatusCode);

        // Опись дела и отбор документов к уничтожению ходят по этим полям.
        builder.HasIndex(x => x.NomenclatureCaseId);
        builder.HasIndex(x => x.DestroyAfterYear);
    }
}
