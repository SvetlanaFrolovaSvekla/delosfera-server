using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndLinkConfiguration : IEntityTypeConfiguration<VndLink>
{
    public void Configure(EntityTypeBuilder<VndLink> builder)
    {
        builder.ToTable("vnd_link");

        builder.HasOne(x => x.SourceVnd)
            .WithMany(x => x.OutgoingLinks)
            .HasForeignKey(x => x.SourceVndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetVnd)
            .WithMany(x => x.IncomingLinks)
            .HasForeignKey(x => x.TargetVndId)
            .OnDelete(DeleteBehavior.Restrict);

        // Удалили редакцию (например, отменили черновик) - упоминания в её тексте теряют смысл.
        builder.HasOne(x => x.SourceRedaction)
            .WithMany()
            .HasForeignKey(x => x.SourceRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Удалили редакцию-цель - ссылка остаётся, но ведёт уже на документ целиком.
        builder.HasOne(x => x.TargetRedaction)
            .WithMany()
            .HasForeignKey(x => x.TargetRedactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.SourceDocumentTarget).HasMaxLength(20);
        builder.Property(x => x.TargetDocumentTarget).HasMaxLength(20);
        builder.Property(x => x.SourceText).HasMaxLength(1000);
        builder.Property(x => x.TargetText).HasMaxLength(1000);
        builder.Property(x => x.SourcePrefix).HasMaxLength(200);
        builder.Property(x => x.SourceSuffix).HasMaxLength(200);
        builder.Property(x => x.TargetPrefix).HasMaxLength(200);
        builder.Property(x => x.TargetSuffix).HasMaxLength(200);
        builder.Property(x => x.LegacyCode).HasMaxLength(50);
    }
}
