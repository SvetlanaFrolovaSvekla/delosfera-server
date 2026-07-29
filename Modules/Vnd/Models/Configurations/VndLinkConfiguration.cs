using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Vnd.Models.Configurations;

public class VndLinkConfiguration : IEntityTypeConfiguration<VndLink>
{
    public void Configure(EntityTypeBuilder<VndLink> builder)
    {
        builder.ToTable("vnd_link");

        builder.HasOne(x => x.SourceVnd)
            .WithMany(x => x.OutgoingLinks)
            .HasForeignKey(x => x.SourceVndId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetVnd)
            .WithMany(x => x.IncomingLinks)
            .HasForeignKey(x => x.TargetVndId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}