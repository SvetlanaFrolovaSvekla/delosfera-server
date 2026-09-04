using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class CoordinationDefaultApproverConfiguration : IEntityTypeConfiguration<CoordinationDefaultApprover>
{
    public void Configure(EntityTypeBuilder<CoordinationDefaultApprover> builder)
    {
        builder.ToTable("vnd_coordination_default_approver");

        builder.Property(x => x.Title).HasMaxLength(300);

        // Порядок этапов должен быть однозначным и сплошным (1..N) - см.
        // CoordinationDefaultApproverService, где Create/Delete/Reorder это поддерживают.
        builder.HasIndex(x => x.Order).IsUnique();

        // СП обязателен для этапа - не даём удалить подразделение, пока на него ссылается
        // хотя бы одна запись справочника (администратор должен сначала переназначить СП).
        builder.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApproverUser)
            .WithMany()
            .HasForeignKey(x => x.ApproverUserId)
            .OnDelete(DeleteBehavior.SetNull); // если пользователя удалили - просто сбрасываем дефолт

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Данные существовавших 4 фиксированных этапов переносятся как обычные (редактируемые)
        // строки справочника - Title/OrgUnitId соответствуют прежним ApprovalStageKind/
        // FixedApprovalOrgUnits. OrgUnitId для Методологии здесь исправлен на корректный (34
        // было занято Юр. управлением, поэтому раньше могла подтягиваться пустая/чужая запись
        // OrganizationUnits при рассинхроне сидов на конкретной БД) - при необходимости
        // администратор поправит СП/название/согласующего через справочник после миграции.
        builder.HasData(
            new
            {
                Id = 1, Title = "Юридическое управление", Order = 1, OrgUnitId = 34,
                ApproverUserId = (int?)16, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 2, Title = "Риск-менеджмент", Order = 2, OrgUnitId = 28,
                ApproverUserId = (int?)14, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 3, Title = "Комплаенс-контроль", Order = 3, OrgUnitId = 5,
                ApproverUserId = (int?)15, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                // Здесь стоит подразделение из сида: на чистой базе портальных
                // записей ещё нет, и ссылаться на них нельзя. Когда синхронизация
                // приведёт «Отдел методологии», подразделение этапа найдёт
                // FixedApprovalUnitResolver по номеру портала, а администратор
                // поправит эту строку через справочник.
                Id = 4, Title = "Методология", Order = 4, OrgUnitId = 33,
                ApproverUserId = (int?)3, CreatedAt = seedDate, UpdatedAt = seedDate
            }
        );
    }
}
