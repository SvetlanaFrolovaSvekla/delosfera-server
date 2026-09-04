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

        builder.HasOne(x => x.ApprovalSheetFile)
            .WithMany()
            .HasForeignKey(x => x.ApprovalSheetFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DisagreementMatrixFile)
            .WithMany()
            .HasForeignKey(x => x.DisagreementMatrixFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.VndRedaction)
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Реквизиты редакции (см. пометку в VndRedaction.cs про переходный период) ───
        builder.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Developer).WithMany().HasForeignKey(x => x.DeveloperId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CuratorDeveloper).WithMany().HasForeignKey(x => x.CuratorDeveloperId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Organ).WithMany().HasForeignKey(x => x.OrganId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SecrecyLevel).WithMany().HasForeignKey(x => x.SecrecyLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── Ответственные исполнители этой редакции (many-to-many с OrganizationUnit) ───
        builder.HasMany(x => x.ResponsibleExecutors)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_redaction_responsible_executor",
                j => j.HasOne<Dictionaries.Models.OrganizationUnit>().WithMany().HasForeignKey("OrganizationUnitId"),
                j => j.HasOne<VndRedaction>().WithMany().HasForeignKey("VndRedactionId"),
                j => { j.ToTable("vnd_redaction_responsible_executor"); });

        // ─── Ключевые слова этой редакции (many-to-many с Keyword) ───
        builder.HasMany(x => x.Keywords)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_redaction_keyword",
                j => j.HasOne<Dictionaries.Models.Keyword>().WithMany().HasForeignKey("KeywordId"),
                j => j.HasOne<VndRedaction>().WithMany().HasForeignKey("VndRedactionId"),
                j => { j.ToTable("vnd_redaction_keyword"); });

        // ─── Рубрики этой редакции (many-to-many с Rubric) ───
        builder.HasMany(x => x.Rubrics)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "vnd_redaction_rubric",
                j => j.HasOne<Dictionaries.Models.Rubric>().WithMany().HasForeignKey("RubricId"),
                j => j.HasOne<VndRedaction>().WithMany().HasForeignKey("VndRedactionId"),
                j => { j.ToTable("vnd_redaction_rubric"); });
    }
}