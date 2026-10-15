using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Ручная правка состава "Доски ВНД" пользователя.</summary>
public enum VndBoardItemMode
{
    /// <summary>Документ добавлен на доску вручную (с СП пользователя напрямую не связан).</summary>
    Pinned = 1,

    /// <summary>Документ связан с СП пользователя автоматически, но пользователь убрал его с доски.</summary>
    Hidden = 2,
}

/// <summary>
/// "Доска ВНД": личная витрина документов пользователя. Основной состав доски вычисляется на лету
/// (ВНД, у которых СП пользователя — разработчик или ответственный исполнитель, см. VndBoardService),
/// а здесь хранятся только ручные поправки к нему: что пользователь добавил сам (Pinned) и что из
/// автоматически подобранного убрал (Hidden). Одна строка на пару (пользователь, документ) — один
/// документ не может быть одновременно добавлен и скрыт.
/// </summary>
public class VndBoardItem
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public VndBoardItemMode Mode { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class VndBoardItemConfiguration : IEntityTypeConfiguration<VndBoardItem>
{
    public void Configure(EntityTypeBuilder<VndBoardItem> b)
    {
        b.ToTable("vnd_board_item");
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
