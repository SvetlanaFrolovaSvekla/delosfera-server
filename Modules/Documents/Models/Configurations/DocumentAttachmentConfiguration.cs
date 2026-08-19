using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.Models.Configurations;

public class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.ToTable("document_attachment");

        builder.HasOne(x => x.Document)
            .WithMany(d => d.Attachments)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DocumentId);
        builder.HasIndex(x => x.Hash);
    }
}
