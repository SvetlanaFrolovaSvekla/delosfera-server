using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRedactionAttachmentConfiguration : IEntityTypeConfiguration<VndRedactionAttachment>
{
    public void Configure(EntityTypeBuilder<VndRedactionAttachment> builder)
    {
        builder.ToTable("vnd_redaction_attachment");

        builder.HasOne(x => x.VndRedaction)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Один и тот же файл не может быть дважды прикреплён к одной редакции
        builder.HasIndex(x => new { x.VndRedactionId, x.FileAttachmentId }).IsUnique();
    }
}