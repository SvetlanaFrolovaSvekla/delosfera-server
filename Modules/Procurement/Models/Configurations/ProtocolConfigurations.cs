using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class ProcurementProtocolConfiguration : IEntityTypeConfiguration<ProcurementProtocol>
{
    public void Configure(EntityTypeBuilder<ProcurementProtocol> b)
    {
        b.ToTable("procurement_protocol");

        // Один протокол на закупку: пересборка обновляет существующий,
        // иначе в деле окажется два документа с разным решением.
        b.HasIndex(x => x.RequestId).IsUnique();

        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.MainAmount).HasPrecision(18, 2);
        b.Property(x => x.ReserveAmount).HasPrecision(18, 2);

        b.HasOne(x => x.Request)
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.MainSupplier)
            .WithMany()
            .HasForeignKey(x => x.MainSupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ReserveSupplier)
            .WithMany()
            .HasForeignKey(x => x.ReserveSupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Rows)
            .WithOne(r => r.Protocol!)
            .HasForeignKey(r => r.ProtocolId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Signatures)
            .WithOne(s => s.Protocol!)
            .HasForeignKey(s => s.ProtocolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProtocolRowConfiguration : IEntityTypeConfiguration<ProtocolRow>
{
    public void Configure(EntityTypeBuilder<ProtocolRow> b)
    {
        b.ToTable("procurement_protocol_row");
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.SupplierTitle).HasMaxLength(400);
        b.HasIndex(x => new { x.ProtocolId, x.Order });
    }
}

public class ProtocolSignatureConfiguration : IEntityTypeConfiguration<ProtocolSignature>
{
    public void Configure(EntityTypeBuilder<ProtocolSignature> b)
    {
        b.ToTable("procurement_protocol_signature");
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.Level).HasConversion<int>();

        // Одна действующая подпись на роль; аннулированные остаются в истории.
        b.HasIndex(x => new { x.ProtocolId, x.Role, x.Revoked });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
