using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.RiskEvents.Models.Configurations;

public class RiskEventConfiguration : IEntityTypeConfiguration<RiskEvent>
{
    public void Configure(EntityTypeBuilder<RiskEvent> b)
    {
        b.ToTable("risk_event");

        b.HasKey(x => x.Id);

        b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ResolutionComment).HasMaxLength(4000);
        b.Property(x => x.LossCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.DirectLossAmount).HasPrecision(18, 2);

        // Инициатор — реальный сотрудник; события не удаляются вместе с ним.
        b.HasOne(x => x.InitiatorUser).WithMany()
            .HasForeignKey(x => x.InitiatorUserId).OnDelete(DeleteBehavior.Restrict);

        // Источники событие переживает: при удалении документа/маршрута ссылка
        // обнуляется, а запись о риске и сумма потерь остаются (регулятивная база).
        b.HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.RouteInstance).WithMany()
            .HasForeignKey(x => x.RouteInstanceId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.RouteStep).WithMany()
            .HasForeignKey(x => x.RouteStepId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.RouteParticipant).WithMany()
            .HasForeignKey(x => x.RouteParticipantId).OnDelete(DeleteBehavior.SetNull);

        // Реестр фильтруют по статусу, типу и дате — под эти запросы индексы.
        b.HasIndex(x => new { x.Status, x.OccurredOn });
        b.HasIndex(x => x.Type);
        b.HasIndex(x => x.DocumentId);
    }
}
