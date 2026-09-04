using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

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

        // Запись справочника, из которой построен этот этап - чисто информационная ссылка
        // (Title/OrgUnitId/ApproverUserId уже сохранены в самом этапе), поэтому при удалении
        // записи справочника просто обнуляем ссылку, не трогая историю согласования.
        builder.HasOne(x => x.CoordinationStage)
            .WithMany()
            .HasForeignKey(x => x.CoordinationStageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ApprovalProcessId, x.Order }).IsUnique();
    }
}