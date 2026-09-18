using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRedactionRevisionSnapshotConfiguration : IEntityTypeConfiguration<VndRedactionRevisionSnapshot>
{
    public void Configure(EntityTypeBuilder<VndRedactionRevisionSnapshot> builder)
    {
        builder.ToTable("vnd_redaction_revision_snapshot");

        builder.HasOne(x => x.VndRedaction)
            .WithMany()
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ApprovalProcess)
            .WithMany(x => x.RedactionSnapshots)
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // Снимки нумеруются последовательно (1,2,3...) в рамках редакции и не могут повторяться.
        builder.HasIndex(x => new { x.VndRedactionId, x.SnapshotNumber }).IsUnique();

        // Restrict — как и на самой VndRedaction (см. VndRedactionConfiguration) — файл снимка
        // нельзя случайно удалить, пока на него ссылается история согласования.
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

        builder.HasOne(x => x.DisagreementMatrixFile)
            .WithMany()
            .HasForeignKey(x => x.DisagreementMatrixFileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
