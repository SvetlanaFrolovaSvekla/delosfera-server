using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Help.Models.Configurations;

public class HelpArticleConfiguration : IEntityTypeConfiguration<HelpArticle>
{
    public void Configure(EntityTypeBuilder<HelpArticle> b)
    {
        b.ToTable("help_article");
        b.Property(x => x.Section).HasConversion<string>();
        b.Property(x => x.TitleRu).HasMaxLength(300);
        b.Property(x => x.TitleKg).HasMaxLength(300);
        b.Property(x => x.RoutePath).HasMaxLength(200);

        // Оглавление всегда читается разделами по порядку — индекс под этот запрос.
        b.HasIndex(x => new {x.Section, x.SortOrder});

        // Справка по экрану ищется по маршруту: без индекса это перебор всех статей
        // на каждой странице системы.
        b.HasIndex(x => x.RoutePath);
    }
}
