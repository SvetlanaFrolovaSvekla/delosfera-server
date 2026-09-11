using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class ActualizationNotificationResponsibleConfiguration
    : IEntityTypeConfiguration<ActualizationNotificationResponsible>
{
    public void Configure(EntityTypeBuilder<ActualizationNotificationResponsible> builder)
    {
        builder.ToTable("vnd_actualization_notification_responsible");

        // Один и тот же сотрудник не назначается на одно СП дважды.
        builder.HasIndex(x => new {x.OrgUnitId, x.UserId}).IsUnique();

        // Чистая связка СП-сотрудник: удаление подразделения или пользователя просто снимает
        // назначение, а не блокируется и не оставляет висящую запись справочника, как у
        // CoordinationDefaultApprover (там кроме связки есть ещё Title/Order).
        builder.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
