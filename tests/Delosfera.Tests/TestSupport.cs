using delosfera_server.Common.Security;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delosfera.Tests;

/// <summary>Общие фабрики для тестов: изолированный in-memory DbContext и реальные сервисы аутентификации.</summary>
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

    // Уникальная БД на каждый тест — тесты не влияют друг на друга.
    public static DelosferaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseInMemoryDatabase($"delosfera-tests-{Guid.NewGuid()}")
            .Options);

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
