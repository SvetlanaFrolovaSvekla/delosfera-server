using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.Models.Configurations;

public class NumeratorConfiguration : IEntityTypeConfiguration<Numerator>
{
    public void Configure(EntityTypeBuilder<Numerator> builder)
    {
        builder.ToTable("numerator");

        builder.Property(x => x.DocumentType).HasConversion<string>();

        builder.HasIndex(x => new { x.DocumentType, x.Scope, x.ScopeKey }).IsUnique();
    }
}
