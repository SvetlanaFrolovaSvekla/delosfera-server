using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRedactionConfiguration : IEntityTypeConfiguration<VndRedaction>
{
    public void Configure(EntityTypeBuilder<VndRedaction> builder)
    {
        builder.ToTable("vnd_redaction");

        // Номер редакции уникален в рамках одного ВНД
        builder.HasIndex(x => new { x.VndId, x.Number }).IsUnique();

        // Код редакции уникален глобально (10062-Р3 и т.п.)
        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.Vnd)
            .WithMany(x => x.Redactions)
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict — чтобы нельзя было случайно удалить файл, пока он привязан к редакции.
        // Удаление файла должно идти через явную бизнес-операцию (удалить редакцию → удалить файлы).
        builder.HasOne(x => x.DocFileRu)
            .WithMany()
            .HasForeignKey(x => x.DocFileRuId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocFileKg)
            .WithMany()
            .HasForeignKey(x => x.DocFileKgId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocFileEn)
            .WithMany()
            .HasForeignKey(x => x.DocFileEnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TidFile)
            .WithMany()
            .HasForeignKey(x => x.TidFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.VndRedaction)
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}