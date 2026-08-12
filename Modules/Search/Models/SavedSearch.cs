using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Search.Models;

/// <summary>
/// Сохранённый фильтр поиска (GEN-04).
///
/// Условия хранятся одним json-полем, а не набором колонок: состав фильтров будет
/// расти вместе с контурами, и каждая новая галочка иначе означала бы миграцию.
/// Разбирает их тот же слой, что и строит, — на стороне базы по ним не ищут.
/// </summary>
public class SavedSearch : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Владелец фильтра: чужие сохранённые запросы в списке не показываются.</summary>
    public int UserId { get; set; }
    public User? User { get; set; }

    public required string Name { get; set; }

    /// <summary>Условия поиска в том же виде, в каком их принимает запрос.</summary>
    public required string Criteria { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> b)
    {
        b.ToTable("saved_search");
        b.Property(x => x.Criteria).HasColumnType("jsonb");

        // У одного сотрудника не бывает двух фильтров с одинаковым названием —
        // иначе в списке не отличить, какой из них открываешь.
        b.HasIndex(x => new {x.UserId, x.Name}).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
