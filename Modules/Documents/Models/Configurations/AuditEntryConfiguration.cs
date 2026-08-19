using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Documents.Models.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entry");

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.At);

        // Неизменяемость (NFR-04) обеспечивается на уровне приложения (только append).
    }
}
