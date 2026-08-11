using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Users.Models.Configurations;

public class SubstitutionConfiguration : IEntityTypeConfiguration<Substitution>
{
    public void Configure(EntityTypeBuilder<Substitution> b)
    {
        b.ToTable("user_substitution");

        // Активное замещение ищется на каждой выдаче задач — индекс под этот запрос.
        b.HasIndex(x => new { x.UserId, x.StartsOn, x.EndsOn });
        b.HasIndex(x => new { x.SubstituteUserId, x.StartsOn, x.EndsOn });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SubstituteUser)
            .WithMany()
            .HasForeignKey(x => x.SubstituteUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
