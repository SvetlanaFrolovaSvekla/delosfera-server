using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Vnd.Models.Configurations;

public class VndApprovalProcessConfiguration : IEntityTypeConfiguration<VndApprovalProcess>
{
    public void Configure(EntityTypeBuilder<VndApprovalProcess> builder)
    {
        builder.ToTable("vnd_approval_process");

        builder.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Redaction)
            .WithMany()
            .HasForeignKey(x => x.RedactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.ApprovalProcess)
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // На одну редакцию - не больше одного процесса согласования
        builder.HasIndex(x => x.RedactionId).IsUnique();
    }
}