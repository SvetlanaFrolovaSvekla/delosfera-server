using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("procurement_supplier");
        b.Property(x => x.Title).HasMaxLength(400);

        // ИНН не у всех поставщиков заполнен, но там где есть — он должен быть один:
        // по нему идут проверки чёрного списка и аффилированности.
        b.HasIndex(x => x.Inn).IsUnique().HasFilter("inn IS NOT NULL");
        b.HasIndex(x => x.IsBlacklisted);
    }
}

public class CommercialProposalConfiguration : IEntityTypeConfiguration<CommercialProposal>
{
    public void Configure(EntityTypeBuilder<CommercialProposal> b)
    {
        b.ToTable("procurement_proposal");
        b.Property(x => x.Price).HasPrecision(18, 2);

        // Один поставщик — одно предложение в заявке: повторное регистрируется правкой,
        // иначе сравнительная таблица дублирует участника.
        b.HasIndex(x => new { x.RequestId, x.SupplierId }).IsUnique();

        b.HasOne(x => x.Request)
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
