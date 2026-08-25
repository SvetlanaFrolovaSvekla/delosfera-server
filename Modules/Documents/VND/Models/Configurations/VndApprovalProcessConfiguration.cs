using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndApprovalProcessConfiguration : IEntityTypeConfiguration<VndApprovalProcess>
{
    public void Configure(EntityTypeBuilder<VndApprovalProcess> builder)
    {
        builder.ToTable("vnd_approval_process");

        builder.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Redaction)
            .WithMany()
            .HasForeignKey(x => x.RedactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.ApprovalProcess)
            .HasForeignKey(x => x.ApprovalProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // На одну редакцию - не больше ОДНОГО АКТИВНОГО процесса согласования. Полностью
        // безусловная уникальность по RedactionId (как было раньше) ломала повторный запуск
        // согласования по той же редакции - а это штатный сценарий: отзыв согласования
        // (CancelAsync) не удаляет старую запись, только помечает её Cancelled, и "актуализация
        // без изменений" может гонять одну и ту же действующую редакцию через согласование
        // несколько раз подряд (см. комментарий в VndApprovalService.StartAsync). Частичный
        // индекс пропускает завершённые/отменённые/отклонённые процессы, поэтому новый запуск
        // по той же редакции больше не падает с 23505 (duplicate key ix_vnd_approval_process_redaction_id).
        builder.HasIndex(x => x.RedactionId)
            .IsUnique()
            .HasFilter(
                $"status NOT IN ({(int)ApprovalProcessStatus.Approved}, " +
                $"{(int)ApprovalProcessStatus.Cancelled}, {(int)ApprovalProcessStatus.Rejected})");
    }
}