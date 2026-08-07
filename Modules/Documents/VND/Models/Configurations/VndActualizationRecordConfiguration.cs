using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.VND.Models.Configurations;

public class VndActualizationRecordConfiguration : IEntityTypeConfiguration<VndActualizationRecord>
{
    public void Configure(EntityTypeBuilder<VndActualizationRecord> builder)
    {
        builder.ToTable("vnd_actualization_record");

        builder.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResponsibleUser)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Быстрый поиск истории конкретного ВНД (сортировка по дате начала)
        // и быстрый поиск "текущего открытого цикла" (PublishedAt IS NULL) в PublishAsync
        builder.HasIndex(x => new { x.VndId, x.StartedAt });
    }
}