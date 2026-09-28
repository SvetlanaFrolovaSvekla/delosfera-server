using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Files.Models.Configurations;

public class AttachmentVersionConfiguration : IEntityTypeConfiguration<AttachmentVersion>
{
    public void Configure(EntityTypeBuilder<AttachmentVersion> builder)
    {
        builder.ToTable("attachment_version");

        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Номер версии уникален в пределах одного вложения: два объекта под одним
        // номером означали бы расхождение истории с фактическим содержимым.
        builder.HasIndex(x => new { x.FileAttachmentId, x.Version }).IsUnique();
    }
}
