using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Obligations.Models.Configurations;

public class RecurringObligationConfiguration : IEntityTypeConfiguration<RecurringObligation>
{
    public void Configure(EntityTypeBuilder<RecurringObligation> builder)
    {
        builder.ToTable("recurring_obligation");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Basis).HasMaxLength(500);

        builder.HasOne(x => x.ResponsibleUser)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResponsibleUnit)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.IsActive, x.Kind });
    }
}

public class ObligationPeriodConfiguration : IEntityTypeConfiguration<ObligationPeriod>
{
    public void Configure(EntityTypeBuilder<ObligationPeriod> builder)
    {
        builder.ToTable("obligation_period");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Comment).HasMaxLength(2000);

        // Вычисляемое свойство в базу не отображается: оно считается по датам и
        // хранить его значило бы завести второй источник правды.
        builder.Ignore(x => x.IsLate);

        builder.HasOne(x => x.Obligation)
            .WithMany(x => x.Periods)
            .HasForeignKey(x => x.ObligationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FulfilledByUser)
            .WithMany()
            .HasForeignKey(x => x.FulfilledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Заседание удаляют редко, но если удалят — период должен остаться и снова
        // стать открытым, а не исчезнуть вместе с доказательством.
        builder.HasOne(x => x.Meeting)
            .WithMany()
            .HasForeignKey(x => x.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);

        // Период обязательства единственен: два периода с одним началом означали бы
        // двойной учёт одного и того же месяца.
        builder.HasIndex(x => new { x.ObligationId, x.PeriodStart }).IsUnique();

        // «Что просрочено» и «что горит на неделе» — ради этого календарь и ведут.
        builder.HasIndex(x => new { x.Status, x.DueDate });
    }
}
