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

        // Цепочка целостности (AUD-1): Hash уникален, каждый Hash используется как PrevHash
        // ровно один раз — уникальный индекс не даёт «раздвоить» цепь незаметно.
        builder.HasIndex(x => x.Hash).IsUnique();

        // Неизменяемость (NFR-04) обеспечивается на уровне приложения (только append),
        // а теперь ещё и доказуема хеш-цепью (AUD-1): любое изменение/удаление обнаружит проверка.
    }
}
