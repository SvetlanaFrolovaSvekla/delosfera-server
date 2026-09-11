using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Notifications.Models.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notification");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Удаление файла (или самого уведомления) не должно требовать ручной чистки на другой
        // стороне — уведомление просто перестаёт показывать вложение (см. Notification.AttachmentFile).
        builder.HasOne(x => x.AttachmentFile)
            .WithMany()
            .HasForeignKey(x => x.AttachmentFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Recipients)
            .WithOne(x => x.Notification)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.CreatedAt);
    }
}
