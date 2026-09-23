using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRedactionApprovalSheetConfiguration : IEntityTypeConfiguration<VndRedactionApprovalSheet>
{
    public void Configure(EntityTypeBuilder<VndRedactionApprovalSheet> builder)
    {
        builder.ToTable("vnd_redaction_approval_sheet");

        builder.HasOne(x => x.VndRedaction)
            .WithMany(x => x.ApprovalSheets)
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull - лист согласования - самостоятельный документ истории редакции, он не должен
        // пропадать вместе с процессом (процессы удаляются только вместе с черновиком ВНД).
        builder.HasOne(x => x.ApprovalProcess)
            .WithMany()
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict - как и у остальных файлов редакции (см. VndRedactionConfiguration).
        builder.HasOne(x => x.FileAttachment)
            .WithMany()
            .HasForeignKey(x => x.FileAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
