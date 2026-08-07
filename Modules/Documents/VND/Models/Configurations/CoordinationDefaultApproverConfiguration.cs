using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class CoordinationDefaultApproverConfiguration : IEntityTypeConfiguration<CoordinationDefaultApprover>
{
    public void Configure(EntityTypeBuilder<CoordinationDefaultApprover> builder)
    {
        builder.ToTable("vnd_coordination_default_approver");

        // Ровно одна строка на каждый фиксированный этап
        builder.HasIndex(x => x.Kind).IsUnique();

        builder.HasOne(x => x.ApproverUser)
            .WithMany()
            .HasForeignKey(x => x.ApproverUserId)
            .OnDelete(DeleteBehavior.SetNull); // если пользователя удалили - просто сбрасываем дефолт

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ApproverUserId = null на старте - админ проставит значения через фронт.
        builder.HasData(
            new { Id = 1, Kind = ApprovalStageKind.Legal, ApproverUserId = 16, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, Kind = ApprovalStageKind.RiskManagement, ApproverUserId = 14, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, Kind = ApprovalStageKind.Compliance, ApproverUserId = 15, CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 4, Kind = ApprovalStageKind.Methodology, ApproverUserId = 3, CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}