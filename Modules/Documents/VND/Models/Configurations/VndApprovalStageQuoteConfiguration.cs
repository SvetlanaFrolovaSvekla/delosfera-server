using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalStageQuoteConfiguration : IEntityTypeConfiguration<VndApprovalStageQuote>
{
    public void Configure(EntityTypeBuilder<VndApprovalStageQuote> builder)
    {
        builder.ToTable("vnd_approval_stage_quote");

        builder.HasOne(x => x.VndApprovalStage)
            .WithMany(x => x.Quotes)
            .HasForeignKey(x => x.VndApprovalStageId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vnd_approval_stage_quote_vnd_approval_stage_id");

        builder.HasIndex(x => new { x.VndApprovalStageId, x.Phase });
    }
}
