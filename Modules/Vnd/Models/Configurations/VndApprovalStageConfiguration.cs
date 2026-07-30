using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Vnd.Models.Configurations;

public class VndApprovalStageConfiguration : IEntityTypeConfiguration<VndApprovalStage>
{
    public void Configure(EntityTypeBuilder<VndApprovalStage> builder)
    {
        builder.ToTable("vnd_approval_stage");

        builder.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApproverUser)
            .WithMany()
            .HasForeignKey(x => x.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ApprovalProcessId, x.Order }).IsUnique();
    }
}