using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Correspondence.Models.Configurations;

public class CorrespondentConfiguration : IEntityTypeConfiguration<Correspondent>
{
    public void Configure(EntityTypeBuilder<Correspondent> builder)
    {
        builder.ToTable("correspondent");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ShortTitle).HasMaxLength(120);
        builder.Property(x => x.TaxId).HasMaxLength(30);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(100);
        builder.Property(x => x.ContactPerson).HasMaxLength(300);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasIndex(x => x.Title);
        builder.HasIndex(x => new { x.Kind, x.IsActive });
    }
}

public class CorrespondenceLetterConfiguration : IEntityTypeConfiguration<CorrespondenceLetter>
{
    public void Configure(EntityTypeBuilder<CorrespondenceLetter> builder)
    {
        builder.ToTable("correspondence_letter");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegNumber).HasMaxLength(50);
        builder.Property(x => x.TheirNumber).HasMaxLength(100);
        builder.Property(x => x.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.Property(x => x.Enclosures).HasMaxLength(1000);
        builder.Property(x => x.Resolution).HasMaxLength(2000);
        builder.Property(x => x.ExecutionNote).HasMaxLength(2000);

        builder.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(subject, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(reg_number, '') || ' ' || coalesce(their_number, ''))",
                stored: true);

        builder.HasIndex(x => x.SearchVector).HasMethod("GIN");

        // IsOverdue отображать не нужно: это метод с параметром «на какой день»,
        // и EF его сам не трогает. Просрочка считается по DueDate и Status.

        builder.HasOne(x => x.Correspondent)
            .WithMany()
            .HasForeignKey(x => x.CorrespondentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResponsibleUser)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResolutionByUser)
            .WithMany()
            .HasForeignKey(x => x.ResolutionByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResponsibleUnit)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.NomenclatureCase)
            .WithMany()
            .HasForeignKey(x => x.NomenclatureCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ответ ссылается на входящее. Удаление входящего не должно уносить ответ:
        // отправленное письмо существует независимо от того, что мы храним.
        builder.HasOne(x => x.InReplyTo)
            .WithMany(x => x.Replies)
            .HasForeignKey(x => x.InReplyToId)
            .OnDelete(DeleteBehavior.Restrict);

        // Номер уникален в пределах книги и года — книги входящих и исходящих разные.
        builder.HasIndex(x => new { x.Direction, x.Year, x.RegNumber })
            .IsUnique()
            .HasFilter("reg_number IS NOT NULL");

        // «Что просрочено» — главный вопрос к книге регистрации.
        builder.HasIndex(x => new { x.Status, x.DueDate });
        builder.HasIndex(x => new { x.Direction, x.RegisteredOn });
        builder.HasIndex(x => x.CorrespondentId);
        builder.HasIndex(x => new { x.Category, x.Status });
    }
}

public class LetterFileConfiguration : IEntityTypeConfiguration<LetterFile>
{
    public void Configure(EntityTypeBuilder<LetterFile> builder)
    {
        builder.ToTable("letter_file");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentHash).HasMaxLength(128);

        builder.HasOne(x => x.Letter)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.File)
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
