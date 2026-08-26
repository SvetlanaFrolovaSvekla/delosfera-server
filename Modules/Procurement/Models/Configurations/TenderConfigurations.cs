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

        b.HasMany(x => x.MeetingChanges)
            .WithOne(x => x.Tender!)
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderMeetingChangeConfiguration : IEntityTypeConfiguration<TenderMeetingChange>
{
    public void Configure(EntityTypeBuilder<TenderMeetingChange> b)
    {
        b.ToTable("procurement_tender_meeting_change");
        b.Property(x => x.Reason).HasMaxLength(500);

        b.HasIndex(x => new { x.TenderId, x.At });

        b.HasOne(x => x.By)
            .WithMany()
            .HasForeignKey(x => x.ByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CommissionVoteConfiguration : IEntityTypeConfiguration<CommissionVote>
{
    public void Configure(EntityTypeBuilder<CommissionVote> b)
    {
        b.ToTable("procurement_commission_vote");
        b.Property(x => x.Choice).HasConversion<int>();

        // Один член комиссии — один голос по заявке. Повторное внесение должно
        // менять уже поставленный голос, а не добавлять второй.
        b.HasIndex(x => new { x.BidId, x.MemberId }).IsUnique();

        b.HasOne(x => x.Bid)
            .WithMany(x => x.Votes)
            .HasForeignKey(x => x.BidId)
            .OnDelete(DeleteBehavior.Cascade);

        // Голоса уходят вместе с членом комиссии: если человека из состава
        // исключили, его голос перестаёт существовать, а не остаётся сиротой.
        b.HasOne(x => x.Member)
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.RecordedBy)
            .WithMany()
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
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
