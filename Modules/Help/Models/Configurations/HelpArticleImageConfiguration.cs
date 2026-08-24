using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Help.Models.Configurations;

public class HelpArticleImageConfiguration : IEntityTypeConfiguration<HelpArticleImage>
{
    public void Configure(EntityTypeBuilder<HelpArticleImage> builder)
    {
        builder.ToTable("help_article_image");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Article)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Файл живёт своей жизнью: удаление статьи убирает связь, но не сам файл.
        builder.HasOne(x => x.File)
            .WithMany()
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);

        // По этому индексу идёт проверка доступа при каждой выдаче картинки.
        builder.HasIndex(x => x.FileId);

        // Один файл в статье один раз — вторая связь ничего не добавляет.
        builder.HasIndex(x => new { x.ArticleId, x.FileId }).IsUnique();
    }
}
