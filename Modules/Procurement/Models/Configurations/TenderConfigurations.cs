using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class TenderConfiguration : IEntityTypeConfiguration<Tender>
{
    public void Configure(EntityTypeBuilder<Tender> b)
    {
        b.ToTable("procurement_tender");
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Request)
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Повторный конкурс ссылается на предыдущий: цепочка попыток должна
        // читаться из карточки, а не восстанавливаться по датам.
        b.HasOne(x => x.PreviousTender)
            .WithMany()
            .HasForeignKey(x => x.PreviousTenderId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Commission)
            .WithOne(m => m.Tender!)
            .HasForeignKey(m => m.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Bids)
            .WithOne(x => x.Tender!)
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommissionMemberConfiguration : IEntityTypeConfiguration<CommissionMember>
{
    public void Configure(EntityTypeBuilder<CommissionMember> b)
    {
        b.ToTable("procurement_commission_member");
        b.Property(x => x.Role).HasConversion<int>();

        // Один сотрудник входит в комиссию один раз: иначе кворум и голоса
        // посчитались бы дважды.
        b.HasIndex(x => new { x.TenderId, x.UserId }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderBidConfiguration : IEntityTypeConfiguration<TenderBid>
{
    public void Configure(EntityTypeBuilder<TenderBid> b)
    {
        b.ToTable("procurement_tender_bid");
        b.Property(x => x.Price).HasPrecision(18, 2);

        b.HasIndex(x => new { x.TenderId, x.SupplierId }).IsUnique();

        b.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
