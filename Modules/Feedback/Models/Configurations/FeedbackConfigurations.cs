using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Feedback.Models.Configurations;

public class FeedbackItemConfiguration : IEntityTypeConfiguration<FeedbackItem>
{
    public void Configure(EntityTypeBuilder<FeedbackItem> builder)
    {
        builder.ToTable("feedback_item");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RoutePath).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PageTitle).HasMaxLength(200);
        builder.Property(x => x.UserAgent).HasMaxLength(400);
        builder.Property(x => x.HandlerComment).HasMaxLength(2000);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HandledByUser)
            .WithMany()
            .HasForeignKey(x => x.HandledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Разбирают сообщения с конца и по состоянию: сначала новые.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // «Что пишут про этот экран» — второй по частоте вопрос после «что нового».
        builder.HasIndex(x => x.RoutePath);
    }
}

public class PageVisitConfiguration : IEntityTypeConfiguration<PageVisit>
{
    public void Configure(EntityTypeBuilder<PageVisit> builder)
    {
        builder.ToTable("page_visit");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoutePath).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.SessionKey).HasMaxLength(64);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Три вопроса, три индекса. Таблица растёт быстрее всех прочих, и отчёт,
        // собранный перебором, начнёт мешать работе тех, за кем он наблюдает.
        builder.HasIndex(x => x.VisitedAt);                       // за период
        builder.HasIndex(x => new { x.UserId, x.VisitedAt });      // кто и когда заходил
        builder.HasIndex(x => new { x.RoutePath, x.VisitedAt });   // какие экраны открывают
    }
}
