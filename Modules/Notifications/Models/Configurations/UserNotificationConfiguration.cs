using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Notifications.Models.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notification");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Одному пользователю одно и то же уведомление доставляется только раз
        builder.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();

        // Частые выборки: "мои непрочитанные", "мои избранные", "мои по категории"
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.IsDeleted });
        builder.HasIndex(x => new { x.UserId, x.IsFavorite, x.IsDeleted });
    }
}