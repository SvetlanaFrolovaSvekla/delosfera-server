using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class ProcurementContractConfiguration : IEntityTypeConfiguration<ProcurementContract>
{
    public void Configure(EntityTypeBuilder<ProcurementContract> b)
    {
        b.ToTable("procurement_contract");
        b.HasIndex(x => x.DocumentId).IsUnique();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Request)
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Протокол и конкурс — основания заключения: при чистке справочников ссылка
        // обнуляется, но договор остаётся с записанной суммой и поставщиком.
        b.HasOne(x => x.Protocol)
            .WithMany()
            .HasForeignKey(x => x.ProtocolId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Tender)
            .WithMany()
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ResponsibleUser)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Acts)
            .WithOne(a => a.Contract!)
            .HasForeignKey(a => a.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeliveryActConfiguration : IEntityTypeConfiguration<DeliveryAct>
{
    public void Configure(EntityTypeBuilder<DeliveryAct> b)
    {
        b.ToTable("procurement_delivery_act");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ContractId, x.Number }).IsUnique();

        b.HasOne(x => x.ApprovedByUnitHead)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUnitHeadId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ApprovedByCurator)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByCuratorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
