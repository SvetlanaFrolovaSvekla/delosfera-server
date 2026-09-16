using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Substitutions.Models.Configurations;

public class SubstitutionRequestConfiguration : IEntityTypeConfiguration<SubstitutionRequest>
{
    public void Configure(EntityTypeBuilder<SubstitutionRequest> b)
    {
        b.HasOne(x => x.InitiatorUser).WithMany()
            .HasForeignKey(x => x.InitiatorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AbsentUser).WithMany()
            .HasForeignKey(x => x.AbsentUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubstituteUser).WithMany()
            .HasForeignKey(x => x.SubstituteUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CommissionChairUser).WithMany()
            .HasForeignKey(x => x.CommissionChairUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AbsentUnit).WithMany()
            .HasForeignKey(x => x.AbsentUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubstituteUnit).WithMany()
            .HasForeignKey(x => x.SubstituteUnitId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.CommissionMembers).WithOne(m => m.Request!)
            .HasForeignKey(m => m.RequestId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Status);
    }
}

public class SubstitutionCommissionMemberConfiguration : IEntityTypeConfiguration<SubstitutionCommissionMember>
{
    public void Configure(EntityTypeBuilder<SubstitutionCommissionMember> b)
    {
        b.HasOne(m => m.User).WithMany()
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
