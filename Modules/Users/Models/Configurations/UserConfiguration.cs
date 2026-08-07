using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Users.Models.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(UserSource.Local)
            .HasSentinel(UserSource.Local);

        builder.Property(x => x.BlockedAt).HasColumnName("blocked_at");
        builder.Property(x => x.BlockedByUserId).HasColumnName("blocked_by_user_id");
        builder.Property(x => x.BlockReason).HasColumnName("block_reason").HasMaxLength(500);

        builder.HasOne(x => x.Position)
            .WithMany()
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OrgUnit)
            .WithMany()
            .HasForeignKey(x => x.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BlockedByUser)
            .WithMany()
            .HasForeignKey(x => x.BlockedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Пароль каждого пользователя = его логин (до @). Хеши вычислены заранее
        // и захардкожены как константы - HasData требует детерминированных значений,
        // а PasswordHasher.HashPassword() каждый раз генерирует новую случайную соль,
        // из-за чего модель считалась бы "меняющейся" при каждой сборке.
        builder.HasData(
            new
            {
                Id = 1, FullName = "Азамат Осмонов", Email = "aosmonov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAELaBsuyKeMFxAB+MULrtZ9MjkT9t5fx0pas/Ozvz63EziFaKREY6cggdvLgNVCY6ag==",
                PositionId = (int?)1, OrgUnitId = (int?)26, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 2, FullName = "Гульнара Асанова", Email = "gasanova@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEBc570K6jxp9Cgl9z7O6LPAv1vSm0hcGL8/DUTnhv75jDPRzcFa8bfagsMXikWWvug==",
                PositionId = (int?)2, OrgUnitId = (int?)34, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 3, FullName = "Бермет Токтосунова", Email = "btoktosunova@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAENcTGN+pr9GDKyGE9w4K5jKdimigO7jzpb+UKIoPOd/ZgnO50Hn1ffr8AYbpFRSf5Q==",
                PositionId = (int?)3, OrgUnitId = (int?)33, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 4, FullName = "Тимур Иманалиев", Email = "timanaliev@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEP0K+N392Olc2GDbaBBKmr+iMP2+9/8p4xm/bMhVOsFGUFBw+56Uwfncnq3D3ioFSg==",
                PositionId = (int?)4, OrgUnitId = (int?)35, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 5, FullName = "Эрлан Сыдыков", Email = "esydykov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEPYYwYIZGmUKII4wk3DdX7VxFfSsdPG1Yxtoaq+OtZIKMC2wDMTQnfAxOfngvOp7Zg==",
                PositionId = (int?)5, OrgUnitId = (int?)36, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 6, FullName = "Айгуль Маматова", Email = "amamatova@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEJsegtPXOp1+XnMfDYn+/my6rUZeZP/mc11xAOs3fgVFJxu8xfdFoQKxjULd0pNp3g==",
                PositionId = (int?)6, OrgUnitId = (int?)37, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 7, FullName = "Бакыт Кадыров", Email = "bkadyrov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEMIfWpsAG+CZv+Ne2uZBrKf1Wce56WoQWK1QkEJ7Mlhy6/CbH7VcSIG22izU03JewQ==",
                PositionId = (int?)7, OrgUnitId = (int?)38, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 8, FullName = "Нурлан Абдиев", Email = "nabdiev@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEJY34tOj9D2kQzEPbP8IzIAcRXY9LG1WvfFAF/x8gXYIJCpkPbQ/pTWvJYwYXEy3yg==",
                PositionId = (int?)8, OrgUnitId = (int?)3, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 9, FullName = "Алия Жумаева", Email = "ajumaeva@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEHqpAAB4JDlfYHk91LymTPuh0fK0lTjEyIqsUskrPHlify//ciM1Pgj+tHYkf8i4Mg==",
                PositionId = (int?)9, OrgUnitId = (int?)32, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 10, FullName = "Руслан Ормонов", Email = "rormonov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEOWaZvoD8t9WLi3j+9ROOKm4VfhoSC41MG0f6nol0WCiRsjmAn6uhIXOuoxdEyiU3w==",
                PositionId = (int?)10, OrgUnitId = (int?)39, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 11, FullName = "Салтанат Ибраева", Email = "sibraeva@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEFbfx4mqL8rO9aGfQnkhjR81i+g55Np6sSjzVvt8+zh2hhSvvEeR4cx6k7aq32NVWw==",
                PositionId = (int?)11, OrgUnitId = (int?)38, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 12, FullName = "Данияр Усенов", Email = "dusenov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEKHOs8yYxWNUVqDlX7az+fTMoaJ+etAefAuscQcbQvaG+3myOORWgkwziGQQ0MNVHQ==",
                PositionId = (int?)12, OrgUnitId = (int?)4, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 13, FullName = "Администратор СЭД", Email = "admin@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEJ22NmtcxUmIhlc2h5MYf+9ras0f2x67OwOIA1JzqpE0EqB10wr/7yYZD1HvYlcEsA==",
                PositionId = (int?)13, OrgUnitId = (int?)3, IsActive = true, LastLoginAt = (DateTime?)null,
                Source = UserSource.Local, CreatedAt = seedDate, UpdatedAt = seedDate
            },
            new
            {
                Id = 14, FullName = "Айбек Нуруев", Email = "anuruev@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEFSX5Y9mFPKRXkdc0ZpOic9JgLOmCyM6M3nS1F25N6k6Wkq6YRY/5/661p4kJbLiIw==",
                PositionId = (int?)5, OrgUnitId = (int?)28,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }, // Риск-менеджмент, пароль: anuruev
            new
            {
                Id = 15, FullName = "Жаныл Эсенова", Email = "jesenova@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEJqdeTyPiLGevQVuajcuy3on8zX2JUZAcez6WwYuW546rr7DEhBtMFYOr/lZHRx5aw==",
                PositionId = (int?)6, OrgUnitId = (int?)5,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }, // Комплаенс-контроль, пароль: jesenova
            new
            {
                Id = 16, FullName = "Марат Бекболотов", Email = "mbekbolotov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEJpz1AWMncL0sXvrT8aX/qcQA8GttDT/hxPkJpF7YziJRmwmvNGWWtFA0jDTlE/rxw==",
                PositionId = (int?)2, OrgUnitId = (int?)34,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }, // Юр. Управление (замена Гульнары), пароль: mbekbolotov
            new
            {
                Id = 17, FullName = "Нурбек Осконов", Email = "noskonov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEKXQ0AW10isOhlDcbGPT3gKxa/FaM3gtb2FxhkLBzurisRAXL0HeBBusit2wDqDOkA==",
                PositionId = (int?)3, OrgUnitId = (int?)33,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }, // Методология (замена Бермет), пароль: noskonov
            new
            {
                Id = 18, FullName = "Айнура Токоева", Email = "atokoeva@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEIPjSgpvjJwzYCkxoEjkiZaopA0lLwFnDpT75Vvr78Y3YX3VGFQKXmXpy2F57M2VJQ==",
                PositionId = (int?)8, OrgUnitId = (int?)3,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            }, // Custom-этап, например от ИБ, пароль: atokoeva
            new
            {
                Id = 19, FullName = "Данил Петров", Email = "dpetrov@keremetbank.kg",
                PasswordHash = "AQAAAAEAAYagAAAAEHCfve+vcccRWGbiuG7dVDwSpv4ep13q1yGPUnZH66qz101wPZ5GV3becS/qgn4PHA==",
                PositionId = (int?)2, OrgUnitId = (int?)37,
                IsActive = true, LastLoginAt = (DateTime?)null, Source = UserSource.Local,
                CreatedAt = seedDate, UpdatedAt = seedDate
            } // Обычный сотрудник-инициатор, пароль: dpetrov
        );

        builder.HasMany(x => x.Roles)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "user_role",
                j => j.HasOne<Role>().WithMany().HasForeignKey("RolesId"),
                j => j.HasOne<User>().WithMany().HasForeignKey("UsersId"),
                j =>
                {
                    j.ToTable("user_role");

                    // Роли: 1-Администратор, 2-Рядовой пользователь, 3-Редактор ВНД, 4-Главный редактор ВНД
                    j.HasData(
                        new { UsersId = 1, RolesId = 2 },
                        new { UsersId = 2, RolesId = 3 },
                        new { UsersId = 3, RolesId = 1 },
                        new { UsersId = 3, RolesId = 4 },
                        new { UsersId = 4, RolesId = 2 },
                        new { UsersId = 5, RolesId = 3 },
                        new { UsersId = 6, RolesId = 3 },
                        new { UsersId = 7, RolesId = 3 },
                        new { UsersId = 8, RolesId = 2 },
                        new { UsersId = 9, RolesId = 2 },
                        new { UsersId = 10, RolesId = 2 },
                        new { UsersId = 11, RolesId = 3 },
                        new { UsersId = 12, RolesId = 3 },
                        new { UsersId = 13, RolesId = 1 },
                        new { UsersId = 14, RolesId = 3 },
                        new { UsersId = 15, RolesId = 3 },
                        new { UsersId = 16, RolesId = 3 },
                        new { UsersId = 17, RolesId = 3 },
                        new { UsersId = 18, RolesId = 2 },
                        new { UsersId = 19, RolesId = 2 }
                    );
                });
    }
}