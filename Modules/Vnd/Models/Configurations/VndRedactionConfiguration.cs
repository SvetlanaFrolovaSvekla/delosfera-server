using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Vnd.Models.Configurations;

public class VndRedactionConfiguration : IEntityTypeConfiguration<VndRedaction>
{
    public void Configure(EntityTypeBuilder<VndRedaction> builder)
    {
        builder.ToTable("vnd_redaction");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1, VndId = 1, DocRu = "Текст редакции на русском языке...", DocKg = "Редакциянын кыргыз тилиндеги тексти...", DocEn = "Revision text in English...", AttachmentIds = new int[0], CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, VndId = 2, DocRu = "Текст редакции на русском языке...", DocKg = "Редакциянын кыргыз тилиндеги тексти...", DocEn = "Revision text in English...", AttachmentIds = new int[0], CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, VndId = 3, DocRu = "Текст редакции на русском языке...", DocKg = "Редакциянын кыргыз тилиндеги тексти...", DocEn = "Revision text in English...", AttachmentIds = new int[0], CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, VndId = 4, DocRu = "Текст редакции на русском языке...", DocKg = "Редакциянын кыргыз тилиндеги тексти...", DocEn = "Revision text in English...", AttachmentIds = new int[0], CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 5, VndId = 5, DocRu = "Текст редакции на русском языке...", DocKg = "Редакциянын кыргыз тилиндеги тексти...", DocEn = "Revision text in English...", AttachmentIds = new int[0], CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}