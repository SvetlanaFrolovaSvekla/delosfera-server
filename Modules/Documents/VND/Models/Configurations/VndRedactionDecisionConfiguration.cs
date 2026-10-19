using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndRedactionDecisionConfiguration : IEntityTypeConfiguration<VndRedactionDecision>
{
    public void Configure(EntityTypeBuilder<VndRedactionDecision> builder)
    {
        builder.ToTable("vnd_redaction_decision");

        builder.Property(x => x.DecisionCode).HasMaxLength(100);
        builder.Property(x => x.ActorName).HasMaxLength(300);
        builder.Property(x => x.ActorPosition).HasMaxLength(300);

        builder.HasOne(x => x.VndRedaction)
            .WithMany(x => x.Decisions)
            .HasForeignKey(x => x.VndRedactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull — решение остаётся в истории документа, даже если учётную запись удалят;
        // ФИО и должность при этом сохранены снимком (ActorName/ActorPosition).
        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.VndRedactionId, x.CreatedAt });
    }
}
