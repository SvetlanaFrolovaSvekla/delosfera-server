using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.Models.Configurations;

public class DocumentLinkConfiguration : IEntityTypeConfiguration<DocumentLink>
{
    public void Configure(EntityTypeBuilder<DocumentLink> builder)
    {
        builder.ToTable("document_link");

        // Restrict на обе стороны — иначе множественные каскадные пути в Postgres.
        builder.HasOne(x => x.FromDocument)
            .WithMany()
            .HasForeignKey(x => x.FromDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToDocument)
            .WithMany()
            .HasForeignKey(x => x.ToDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FromDocumentId, x.ToDocumentId, x.LinkType }).IsUnique();
    }
}
