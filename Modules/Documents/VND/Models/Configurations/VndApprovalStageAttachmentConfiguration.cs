using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalStageAttachmentConfiguration : IEntityTypeConfiguration<VndApprovalStageAttachment>
{
    public void Configure(EntityTypeBuilder<VndApprovalStageAttachment> builder)
    {
        builder.ToTable("vnd_approval_stage_attachment");

        builder.HasOne(x => x.VndApprovalStage)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.VndApprovalStageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Один и тот же файл не может быть дважды прикреплён к одной и той же фазе решения
        builder.HasIndex(x => new { x.VndApprovalStageId, x.Phase, x.FileAttachmentId }).IsUnique();
    }
}
