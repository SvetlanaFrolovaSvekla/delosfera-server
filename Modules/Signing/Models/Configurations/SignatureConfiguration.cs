using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Signing.Models.Configurations;

public class SignatureConfiguration : IEntityTypeConfiguration<Signature>
{
    public void Configure(EntityTypeBuilder<Signature> b)
    {
        b.ToTable("signature");
        b.Property(x => x.Level).HasConversion<string>();
        b.HasIndex(x => x.DocumentAttachmentId);
        b.HasIndex(x => x.DocumentId);
    }
}
