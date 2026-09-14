using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalPhaseRoundStageDecisionConfiguration
    : IEntityTypeConfiguration<VndApprovalPhaseRoundStageDecision>
{
    public void Configure(EntityTypeBuilder<VndApprovalPhaseRoundStageDecision> builder)
    {
        builder.ToTable("vnd_approval_phase_round_stage_decision");

        builder.HasOne(x => x.VndApprovalPhaseRound)
            .WithMany(x => x.StageDecisions)
            .HasForeignKey(x => x.VndApprovalPhaseRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ссылка на сам этап - только для отображения (кто именно решал), жизненным циклом
        // владеет круг (VndApprovalPhaseRoundId), поэтому Restrict: этап в рамках одного и
        // того же процесса согласования не удаляется отдельно от процесса, а когда удаляется
        // процесс целиком - каскад уже приходит через VndApprovalPhaseRound.
        builder.HasOne(x => x.VndApprovalStage)
            .WithMany()
            .HasForeignKey(x => x.VndApprovalStageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Один этап - не больше одного снимка решения в рамках одного круга.
        builder.HasIndex(x => new { x.VndApprovalPhaseRoundId, x.VndApprovalStageId }).IsUnique();
    }
}
