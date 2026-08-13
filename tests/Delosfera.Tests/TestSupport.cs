using delosfera_server.Common.Security;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delosfera.Tests;

/// <summary>Общие фабрики для тестов: сервисы аутентификации и сидовые данные.</summary>
internal static class TestSupport
{
    public static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-长-enough-0123456789ABCDEF",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:AccessTokenExpiryMinutes"] = "30",
            ["Jwt:RefreshTokenExpiryDays"] = "30",
        }).Build();

    // Базу выдаёт PostgresFixture: копию шаблона под каждый тест. In-memory провайдер
    // модель не держит (jsonb, tsvector, вычисляемые колонки), и подменять им настоящую
    // базу — значит проверять не то поведение, которое будет в банке.

    public static AuthService NewAuthService(DelosferaDbContext db) =>
        new(db, new UserPasswordHasher(), new JwtTokenService(Config()),
            new DisabledDirectory(), NewPasswordPolicy(), Config());

    /// <summary>
    /// Парольная политика с настройками по умолчанию. Тесты входа проверяют вход,
    /// а не требования к паролю, поэтому политика берётся штатная.
    /// </summary>
    public static IPasswordPolicy NewPasswordPolicy() =>
        new PasswordPolicy(Microsoft.Extensions.Options.Options.Create(new PasswordPolicyOptions()));

    /// <summary>
    /// Служба каталогов, выключенная в тестах: доменный вход требует живого LDAP,
    /// и подменять его сетевым моком ради тестов локального входа незачем.
    /// </summary>
    private sealed class DisabledDirectory : ILdapDirectory
    {
        public bool Enabled => false;

        public Task<List<DirectoryEntry>> ListUsersAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("Интеграция со службой каталогов выключена");

        public Task<DirectoryEntry?> AuthenticateAsync(string login, string password, CancellationToken ct = default) =>
            throw new InvalidOperationException("Интеграция со службой каталогов выключена");
    }

    /// <summary>
    /// Заводит ВНД со ссылками на реальные справочники базы.
    ///
    /// На настоящей базе внешние ключи проверяются: нули в TypeId, DeveloperId,
    /// OrganId и SecrecyLevelId её не проходят. In-memory провайдер это пропускал,
    /// и тесты годами ссылались на несуществующие записи.
    /// </summary>
    public static VndDocument SeedVnd(DelosferaDbContext db, VndStatus status, int createdByUserId)
    {
        EnsureUser(db, createdByUserId);

        var vnd = new VndDocument
        {
            // Код уникален в базе: сидовые ВНД уже занимают свои, поэтому берём случайный.
            Code = $"TEST-{Guid.NewGuid():N}"[..12],
            TitleRu = "Тестовый ВНД",
            Status = status,
            CreatedByUserId = createdByUserId,
            TypeId = db.TypesVnd.OrderBy(x => x.Id).First().Id,
            DeveloperId = db.OrganizationUnits.OrderBy(x => x.Id).First().Id,
            OrganId = db.ApprovalBodies.OrderBy(x => x.Id).First().Id,
            SecrecyLevelId = db.SecurityLevels.OrderBy(x => x.Id).First().Id,
        };

        db.VndDocuments.Add(vnd);
        db.SaveChanges();

        return vnd;
    }

    /// <summary>
    /// Гарантирует существование пользователя с заданным идентификатором.
    ///
    /// Тесты действуют от лица условных сотрудников (100, 200…), а на настоящей базе
    /// внешние ключи проверяются: ссылка на несуществующего автора её не проходит.
    /// </summary>
    public static void EnsureUser(DelosferaDbContext db, int userId)
    {
        if (db.Users.Any(u => u.Id == userId)) return;

        db.Users.Add(new User
        {
            Id = userId,
            FullName = $"Тестовый сотрудник {userId}",
            Email = $"user-{userId}-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Запись файла в хранилище. У редакции ВНД ссылка на файл обязательная,
    /// и на настоящей базе она проверяется внешним ключом.
    /// </summary>
    public static FileAttachment SeedFile(DelosferaDbContext db, int uploadedByUserId)
    {
        EnsureUser(db, uploadedByUserId);

        var file = new FileAttachment
        {
            OriginalFileName = "редакция.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            SizeBytes = 1024,
            StorageKey = Guid.NewGuid().ToString("N"),
            UploadedByUserId = uploadedByUserId,
        };

        db.FileAttachments.Add(file);
        db.SaveChanges();

        return file;
    }

    /// <summary>Сотрудник, от имени которого действует тест.</summary>
    public static User SeedActor(DelosferaDbContext db)
    {
        var user = new User
        {
            FullName = "Тестовый сотрудник",
            Email = $"actor-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        db.SaveChanges();

        return user;
    }

    /// <summary>Создаёт активного пользователя с ролью и заданным паролем.</summary>
    public static User SeedUser(DelosferaDbContext db, string email, string password,
        bool isActive = true, DateTime? blockedAt = null)
    {
        var hasher = new UserPasswordHasher();
        var role = new Role { TitleRu = "Тестовая роль", PermissionCodes = [11, 13] };
        var user = new User
        {
            FullName = "Тест Тестов",
            Email = email,
            PasswordHash = hasher.Hash(password),
            IsActive = isActive,
            BlockedAt = blockedAt,
            Roles = new List<Role> { role },
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
