using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Vnd.Models.Configurations;

public class VndActualizationRequestConfiguration : IEntityTypeConfiguration<VndActualizationRequest>
{
    public void Configure(EntityTypeBuilder<VndActualizationRequest> builder)
    {
        builder.ToTable("vnd_actualization_request");

        builder.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DecidedByUser)
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Быстрый поиск pending-заявок по конкретному ВНД/пользователю
        builder.HasIndex(x => new { x.VndId, x.RequestedByUserId, x.Status });
    }
}