using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// "Избранное" пользователя в реестре ВНД: звёздочка на странице документа и вкладка
/// "Избранное" в реестре. Личная отметка — у каждого пользователя свой список, на других не
/// влияет. Одна строка на пару (пользователь, документ).
/// </summary>
public class VndFavorite
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class VndFavoriteConfiguration : IEntityTypeConfiguration<VndFavorite>
{
    public void Configure(EntityTypeBuilder<VndFavorite> b)
    {
        b.ToTable("vnd_favorite");
        b.HasKey(x => new { x.UserId, x.VndId });
        b.HasIndex(x => x.VndId);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Vnd)
            .WithMany()
            .HasForeignKey(x => x.VndId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
