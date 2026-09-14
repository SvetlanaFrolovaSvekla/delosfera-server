using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalPhaseRoundConfiguration : IEntityTypeConfiguration<VndApprovalPhaseRound>
{
    public void Configure(EntityTypeBuilder<VndApprovalPhaseRound> builder)
    {
        builder.ToTable("vnd_approval_phase_round");

        builder.HasOne(x => x.ApprovalProcess)
            .WithMany(x => x.PhaseRounds)
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // Круги фазы нумеруются последовательно (1,2,3...) и не могут повторяться в рамках
        // одного процесса согласования.
        builder.HasIndex(x => new { x.ApprovalProcessId, x.Phase, x.RoundNumber }).IsUnique();
    }
}
