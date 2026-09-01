using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRepeatCommentAttachmentConfiguration : IEntityTypeConfiguration<VndRepeatCommentAttachment>
{
    public void Configure(EntityTypeBuilder<VndRepeatCommentAttachment> builder)
    {
        builder.ToTable("vnd_repeat_comment_attachment");

        builder.HasOne(x => x.VndApprovalProcess)
            .WithMany(x => x.RepeatInitiatorCommentAttachments)
            .HasForeignKey(x => x.VndApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Один и тот же файл не может быть дважды прикреплён к комментарию одного процесса
        builder.HasIndex(x => new { x.VndApprovalProcessId, x.FileAttachmentId }).IsUnique();
    }
}
