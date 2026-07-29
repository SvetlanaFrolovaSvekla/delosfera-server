using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("dictionary_position");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new Position { Id = 1,  TitleRu = "Гл. специалист", TitleEn = "Chief Specialist", TitleKg = "Башкы адис", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 2,  TitleRu = "Юрисконсульт", TitleEn = "Legal Counsel", TitleKg = "Юрисконсульт", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 3,  TitleRu = "Методолог", TitleEn = "Methodologist", TitleKg = "Методолог", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 4,  TitleRu = "Специалист по закупкам", TitleEn = "Procurement Specialist", TitleKg = "Сатып алуулар боюнча адис", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 5,  TitleRu = "Зам. Председателя Правления", TitleEn = "Deputy Chairman of the Management Board", TitleKg = "Башкарма төрагасынын орун басары", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 6,  TitleRu = "Делопроизводитель", TitleEn = "Records Clerk", TitleKg = "Иш кагаздарын жүргүзүүчү", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 7,  TitleRu = "Начальник управления", TitleEn = "Head of Department", TitleKg = "Башкарманын башчысы", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 8,  TitleRu = "Начальник департамента", TitleEn = "Head of Division", TitleKg = "Департаменттин башчысы", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 9,  TitleRu = "HR-директор", TitleEn = "HR Director", TitleKg = "HR-директор", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 10, TitleRu = "Начальник отдела", TitleEn = "Head of Unit", TitleKg = "Бөлүм башчысы", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 11, TitleRu = "Главный бухгалтер", TitleEn = "Chief Accountant", TitleKg = "Башкы бухгалтер", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 12, TitleRu = "Директор казначейства", TitleEn = "Treasury Director", TitleKg = "Казыналык директору", CreatedAt = seedDate, UpdatedAt = seedDate },
            new Position { Id = 13, TitleRu = "Администратор", TitleEn = "Administrator", TitleKg = "Администратор", CreatedAt = seedDate, UpdatedAt = seedDate }
        );
    }
}