using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndDisagreementMatrixRowConfiguration : IEntityTypeConfiguration<VndDisagreementMatrixRow>
{
    public void Configure(EntityTypeBuilder<VndDisagreementMatrixRow> builder)
    {
        builder.ToTable("vnd_disagreement_matrix_row");

        builder.HasOne(x => x.ApprovalProcess)
            .WithMany(x => x.DisagreementMatrixRows)
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}