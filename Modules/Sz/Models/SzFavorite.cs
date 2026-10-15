using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// «Избранное» пользователя в реестре СЗ (по образцу <c>VndFavorite</c>): звёздочка в карточке
/// и в строке реестра, вкладка «Избранное». Личная отметка — у каждого пользователя свой
/// список, на других не влияет. Одна строка на пару (пользователь, записка).
/// </summary>
public class SzFavorite
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int SzDocumentId { get; set; }
    public SzDocument? SzDocument { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class SzFavoriteConfiguration : IEntityTypeConfiguration<SzFavorite>
{
    public void Configure(EntityTypeBuilder<SzFavorite> b)
    {
        // Имена заданы явно, а не конвенцией: миграция написана вручную, и снапшот модели
        // должен совпасть с ней буква в букву — иначе Migrate() на старте сочтёт модель изменённой.
        b.ToTable("sz_favorite");
        b.HasKey(x => new { x.UserId, x.SzDocumentId }).HasName("pk_sz_favorite");

        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SzDocumentId).HasColumnName("sz_document_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");

        b.HasIndex(x => x.SzDocumentId).HasDatabaseName("ix_sz_favorite_sz_document_id");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sz_favorite_user_user_id");

        b.HasOne(x => x.SzDocument)
            .WithMany()
            .HasForeignKey(x => x.SzDocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sz_favorite_sz_document_sz_document_id");
    }
}
