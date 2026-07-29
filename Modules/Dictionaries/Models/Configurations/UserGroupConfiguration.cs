using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Dictionaries.Models.Configurations;

public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> builder)
    {
        builder.ToTable("dictionary_user_group");

        builder.Property(x => x.TitleRu).HasColumnName("title_ru");
        builder.Property(x => x.TitleEn).HasColumnName("title_en");
        builder.Property(x => x.TitleKg).HasColumnName("title_kg");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new { Id = 1, TitleRu = "Согласующие ВНД", TitleEn = "VND Approvers", TitleKg = "ВНДди макулдаштыруучулар", CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 2, TitleRu = "Редакторы ВНД", TitleEn = "VND Editors", TitleKg = "ВНД редакторлору", CreatedAt = seedDate, UpdatedAt = seedDate },
            new { Id = 3, TitleRu = "ИТ-администраторы", TitleEn = "IT Administrators", TitleKg = "ИТ-администраторлор", CreatedAt = seedDate, UpdatedAt = seedDate }
        );

        builder.HasMany(x => x.Users)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "user_group_member",
                j => j.HasOne<Users.Models.User>().WithMany().HasForeignKey("UsersId"),
                j => j.HasOne<UserGroup>().WithMany().HasForeignKey("UserGroupId"),
                j =>
                {
                    j.ToTable("user_group_member");

                    // Порядок пользователей по сидингу: 1-Осмонов, 2-Асанова, 3-Токтосунова,
                    // 4-Иманалиев, 5-Сыдыков, 6-Маматова, 7-Кадыров, 8-Абдиев,
                    // 9-Жумаева, 10-Ормонов, 11-Ибраева, 12-Усенов, 13-Администратор

                    j.HasData(
                        // Группа 1 — Согласующие ВНД: Сыдыков, Маматова, Кадыров, Ибраева, Усенов
                        new { UserGroupId = 1, UsersId = 5 },
                        new { UserGroupId = 1, UsersId = 6 },
                        new { UserGroupId = 1, UsersId = 7 },
                        new { UserGroupId = 1, UsersId = 11 },
                        new { UserGroupId = 1, UsersId = 12 },

                        // Группа 2 — Редакторы ВНД: Токтосунова, Сыдыков, Маматова, Кадыров, Ибраева, Усенов
                        new { UserGroupId = 2, UsersId = 3 },
                        new { UserGroupId = 2, UsersId = 5 },
                        new { UserGroupId = 2, UsersId = 6 },
                        new { UserGroupId = 2, UsersId = 7 },
                        new { UserGroupId = 2, UsersId = 11 },
                        new { UserGroupId = 2, UsersId = 12 },

                        // Группа 3 — ИТ-администраторы: Абдиев, Администратор СЭД
                        new { UserGroupId = 3, UsersId = 8 },
                        new { UserGroupId = 3, UsersId = 13 }
                    );
                });
    }
}