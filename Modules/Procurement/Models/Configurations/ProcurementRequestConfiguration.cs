using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Procurement.Models.Configurations;

public class ProcurementRequestConfiguration : IEntityTypeConfiguration<ProcurementRequest>
{
    public void Configure(EntityTypeBuilder<ProcurementRequest> b)
    {
        b.ToTable("procurement_request");

        // Поисковый вектор по предмету, обоснованию и позиции плана — вычисляется базой (GEN-04).
        b.Property(x => x.SearchVector)
            .HasComputedColumnSql(
                "to_tsvector('russian', coalesce(subject, '') || ' ' || coalesce(justification, '') || ' ' || coalesce(plan_item, ''))",
                stored: true);

        b.HasIndex(x => x.SearchVector).HasMethod("GIN");

        // Заявка 1:1 с карточкой документа: номер и статус живут там (GEN-05).
        b.HasIndex(x => x.DocumentId).IsUnique();

        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.SubjectKind).HasConversion<int>();
        b.Property(x => x.ApprovalAuthority).HasConversion<int>();
        b.Property(x => x.Subject).HasMaxLength(500);

        b.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Method)
            .WithMany()
            .HasForeignKey(x => x.MethodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Правило матрицы хранится ради воспроизводимости решения — при чистке
        // справочника ссылка обнуляется, но заявка остаётся с записанным составом согласования.
        b.HasOne(x => x.MatrixRule)
            .WithMany()
            .HasForeignKey(x => x.MatrixRuleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Позицию Плана, на которую уже сослались заявки, удалять нельзя: иначе
        // отчёт об исполнении потеряет и план, и связь с фактом.
        b.HasOne(x => x.PlanItemRef)
            .WithMany()
            .HasForeignKey(x => x.PlanItemId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.InitiatorUnit)
            .WithMany()
            .HasForeignKey(x => x.InitiatorUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.CuratorUser)
            .WithMany()
            .HasForeignKey(x => x.CuratorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
