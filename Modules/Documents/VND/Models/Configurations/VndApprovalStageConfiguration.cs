using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalStageConfiguration : IEntityTypeConfiguration<VndApprovalStage>
{
    public void Configure(EntityTypeBuilder<VndApprovalStage> builder)
    {
        builder.ToTable("vnd_approval_stage");

        // Токен конкурентности на системном столбце Postgres xmin — не требует миграции/нового
        // столбца. Без него фоновая обработка таймаутов (VndApprovalService.ProcessTimeoutsAsync)
        // могла молча перезаписать решение, которое согласующий только что принял сам
        // (POST .../decide) параллельно с этим же проходом: она читает этап как Pending и в
        // конце сохраняет автоакцепт по таймауту безусловным UPDATE, не заметив, что решение уже
        // изменилось. С xmin-токеном такое сохранение вместо этого провалится с
        // DbUpdateConcurrencyException (см. TrySaveTimeoutBatchAsync), и решение пользователя не
        // теряется.
        //
        // Настроено вручную через теневое свойство, а не через .UseXminAsConcurrencyToken() —
        // этот метод расширения Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) здесь не резолвится
        // (CS1061), похоже на рассинхрон версий пакета с Microsoft.EntityFrameworkCore 10.0.10.
        // Ниже — ровно то же самое, что делает сам провайдер под капотом, так что при желании
        // после починки версий пакетов это можно будет заменить обратно на один вызов.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApproverUser)
            .WithMany()
            .HasForeignKey(x => x.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Запись справочника, из которой построен этот этап - чисто информационная ссылка
        // (Title/OrgUnitId/ApproverUserId уже сохранены в самом этапе), поэтому при удалении
        // записи справочника просто обнуляем ссылку, не трогая историю согласования.
        builder.HasOne(x => x.CoordinationStage)
            .WithMany()
            .HasForeignKey(x => x.CoordinationStageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ApprovalProcessId, x.Order }).IsUnique();
    }
}