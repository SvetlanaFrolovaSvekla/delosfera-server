using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class ProcurementPlanConfiguration : IEntityTypeConfiguration<ProcurementPlan>
{
    public void Configure(EntityTypeBuilder<ProcurementPlan> b)
    {
        b.ToTable("procurement_plan");

        // План один на год: иначе заявки ссылались бы на разные версии одного года.
        b.HasIndex(x => x.Year).IsUnique();
        b.Property(x => x.Status).HasConversion<int>();

        b.HasMany(x => x.Items)
            .WithOne(i => i.Plan!)
            .HasForeignKey(i => i.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProcurementPlanItemConfiguration : IEntityTypeConfiguration<ProcurementPlanItem>
{
    public void Configure(EntityTypeBuilder<ProcurementPlanItem> b)
    {
        b.ToTable("procurement_plan_item");
        b.Property(x => x.PlannedAmount).HasPrecision(18, 2);
        b.Property(x => x.SubjectKind).HasConversion<int>();

        // Код позиции уникален внутри года — по нему заявка ссылается на план.
        b.HasIndex(x => new { x.PlanId, x.Code }).IsUnique();

        b.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GuaranteeConfiguration : IEntityTypeConfiguration<Guarantee>
{
    public void Configure(EntityTypeBuilder<Guarantee> b)
    {
        b.ToTable("procurement_guarantee");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.Form).HasConversion<int>();

        // Возврат контролируется по сроку действия — индекс под выборку истекающих.
        b.HasIndex(x => new { x.ValidUntil, x.ReturnedOn });

        b.HasOne(x => x.Tender)
            .WithMany()
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ReturnedBy)
            .WithMany()
            .HasForeignKey(x => x.ReturnedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProcurementClaimConfiguration : IEntityTypeConfiguration<ProcurementClaim>
{
    public void Configure(EntityTypeBuilder<ProcurementClaim> b)
    {
        b.ToTable("procurement_claim");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
